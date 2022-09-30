using FluentAssertions;
using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Rx.Net.StateMachine.Tests
{
    class UserModel
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime BirthDate { get; set; }
    }
    public class BotRegistrationTests : IDisposable
    {
        private readonly SessionStateDataStore _dataStore;
        private readonly WorkflowResolver _workflowResolver;
        private readonly WorkflowManager _workflowManager;
        private readonly IDisposable _messagesHandling;
        private readonly BotFake _botFake;

        private BehaviorSubject<ConcurrentDictionary<Guid, HashSet<int>>> _handledMessages = new BehaviorSubject<ConcurrentDictionary<Guid, HashSet<int>>>(new ConcurrentDictionary<Guid, HashSet<int>>());

        public BotRegistrationTests()
        {
            _botFake = new BotFake();
            _dataStore = new SessionStateDataStore();
            _workflowResolver = new WorkflowResolver(new BotRegistrationWorkflowFactory(_botFake));

            _workflowManager = new WorkflowManager(
                new JsonSerializerOptions(),
                () => new SessionStateUnitOfWork(_dataStore),
                _workflowResolver
            );

            _messagesHandling = _botFake.UserMessages.SelectAsync(m => HandleUserMessage(m)).Merge().Subscribe();
        }

        public void Dispose()
        {
            _handledMessages.OnCompleted();
            _handledMessages.Dispose();
            _messagesHandling.Dispose();
        }

        [Fact]
        public async Task Should_Return_User_Information()
        {
            var boris = Guid.NewGuid();
            int messageId = await _botFake.SendUserMessage(boris, "/Start");
            await WaitUntilHandled(boris, messageId);

            var botMessages = _botFake.ReadNewBotMessageTexts(boris);
            botMessages.Should().BeEquivalentTo("Hello, please follow steps to pass registration process", "Please enter your first name");

            messageId = await _botFake.SendUserMessage(boris, "Boris");
            await WaitUntilHandled(boris, messageId);
            botMessages = _botFake.ReadNewBotMessageTexts(boris);
            botMessages.Should().BeEquivalentTo("Please enter your last name");

            messageId = await _botFake.SendUserMessage(boris, "Sotsky");
            await WaitUntilHandled(boris, messageId);
            botMessages = _botFake.ReadNewBotMessageTexts(boris);
            botMessages.Should().BeEquivalentTo("Please enter your birth date");

            messageId = await _botFake.SendUserMessage(boris, new DateTime(1987, 6, 23).ToShortDateString());
            await WaitUntilHandled(boris, messageId);
            botMessages = _botFake.ReadNewBotMessageTexts(boris);
            var lastMessage = botMessages.Single();
            lastMessage.Should().Contain(boris.ToString());
            lastMessage.Should().Contain("Boris");
            lastMessage.Should().Contain("Sotsky");
            lastMessage.Should().Contain("1987");
        }

        [Fact]
        public async Task Should_Ask_To_Reenter_FirstName_If_Not_Valid()
        {
            var boris = Guid.NewGuid();
            int messageId = await _botFake.SendUserMessage(boris, "/Start");
            await WaitUntilHandled(boris, messageId);

            var botMessages = _botFake.ReadNewBotMessageTexts(boris);
            botMessages.Should().BeEquivalentTo("Hello, please follow steps to pass registration process", "Please enter your first name");

            messageId = await _botFake.SendUserMessage(boris, "   ");
            await WaitUntilHandled(boris, messageId);
            botMessages = _botFake.ReadNewBotMessageTexts(boris);
            botMessages.Should().BeEquivalentTo("Oops first name is not valid, please try again", "Please enter your first name");

            messageId = await _botFake.SendUserMessage(boris, " ");
            await WaitUntilHandled(boris, messageId);
            botMessages = _botFake.ReadNewBotMessageTexts(boris);
            botMessages.Should().BeEquivalentTo("Oops first name is not valid, please try again", "Please enter your first name");

            messageId = await _botFake.SendUserMessage(boris, "Boris");
            await WaitUntilHandled(boris, messageId);
            botMessages = _botFake.ReadNewBotMessageTexts(boris);
            botMessages.Should().BeEquivalentTo("Please enter your last name");
        }

        private async Task HandleUserMessage(BotFrameworkMessage message)
        {
            var userContext = new UserContext { UserId = message.UserId };

            if (string.Equals(message.Text, "/start", StringComparison.OrdinalIgnoreCase))
                await _workflowManager.StartHandle(BotRegistrationWorkflowFactory.Id, userContext);
            else
                await _workflowManager.HandleEvent(message, userContext);

            var handledMessages = _handledMessages.Value;
            handledMessages.AddOrUpdate(message.UserId, _ => new HashSet<int> { message.MessageId }, (_, hs) =>
            {
                hs.Add(message.MessageId);
                return hs;
            });

            _handledMessages.OnNext(handledMessages);
        }

        private Task WaitUntilHandled(Guid userId, int messageId)
        {
            return _handledMessages
                .Where(hm => hm.TryGetValue(userId, out var handled) && handled.Contains(messageId))
                .Take(1)
                .ToTask();
        }

        // Workflow: https://www.figma.com/file/WPqeeRL8EjiH1rzXT1os7o/User-Registration-Case?node-id=0%3A1
        class BotRegistrationWorkflowFactory : WorkflowFactory<UserModel>
        {
            BotFake _botFake;

            public const string Id = "bot-registration";
            public override string WorkflowId => Id;

            public BotRegistrationWorkflowFactory(BotFake botFake) => _botFake = botFake;

            public override IObservable<UserModel> GetResult(StateMachineScope scope)
            {
                var ctx = scope.GetContext<UserContext>();
                return Observable.FromAsync(() => _botFake.SendBotMessage(ctx.UserId, "Hello, please follow steps to pass registration process"))
                    .Select(_ => new UserModel { Id = ctx.UserId })
                    .Persist(scope, "UserId")
                    .SelectAsync(async user => GetFirstName(await scope.BeginRecursiveScope("FirstName")).Select(firstName =>
                    {
                        user.FirstName = firstName;
                        return user;
                    }))
                    .Concat()
                    .Concat()
                    .Persist(scope, "FirstName")
                    .Select(async user => GetLastName(await scope.BeginRecursiveScope("LastName")).Select(lastName =>
                    {
                        user.LastName = lastName;
                        return user;
                    }))
                    .Concat()
                    .Concat()
                    .Persist(scope, "LastName")
                    .Select(async user => GetBirthDate(await scope.BeginRecursiveScope("BirthDate")).Select(birthDate =>
                    {
                        user.BirthDate = birthDate;
                        return user;
                    }))
                    .Concat()
                    .Concat()
                    .SelectAsync(async user =>
                    {
                        await _botFake.SendBotMessage(ctx.UserId, $"You was successfuly registered: {JsonSerializer.Serialize(user)}");

                        return user;
                    })
                    .Concat();
            }

            private IObservable<string> RequestStringInput(StateMachineScope scope, string displayName, string stateName, Func<string, ValidationResult> validate)
            {
                var ctx = scope.GetContext<UserContext>();
                return Observable.FromAsync(() => _botFake.SendBotMessage(ctx.UserId, $"Please enter your {displayName}"))
                    .Persist(scope, $"Ask{stateName}")
                    .StopAndWait().For<BotFrameworkMessage>(scope, "MessageReceived")
                    .Select(message =>
                    {
                        string text = message.Text;
                        var validationResult = validate(text);
                        if (validationResult == ValidationResult.Success)
                            return StateMachineObservableExtensions.Of(message.Text);

                        return Observable.FromAsync(() => _botFake.SendBotMessage(ctx.UserId, validationResult.ErrorMessage))
                                    .Persist(scope, $"Invalid{stateName}")
                                    .IncreaseRecoursionDepth(scope)
                                    .Select(_ => GetFirstName(scope))
                                    .Concat();
                    }).Concat();
            }

            private IObservable<string> GetFirstName(StateMachineScope scope)
            {
                return RequestStringInput(scope, "first name", "FirstName", s =>
                {
                    if (string.IsNullOrWhiteSpace(s))
                        return new ValidationResult("Oops first name is not valid, please try again");

                    return ValidationResult.Success;
                });
            }

            private IObservable<string> GetLastName(StateMachineScope scope)
            {
                return RequestStringInput(scope, "last name", "LastName", s =>
                {
                    if (string.IsNullOrWhiteSpace(s))
                        return new ValidationResult("Oops last name is not valid, please try again");

                    return ValidationResult.Success;
                });
            }

            private IObservable<DateTime> GetBirthDate(StateMachineScope scope)
            {
                return RequestStringInput(scope, "birth date", "BirthDate", s =>
                {
                    if (string.IsNullOrWhiteSpace(s))
                        return new ValidationResult("Oops birth date is not valid, please try again");
                    if (!DateTime.TryParse(s, out var birthDate))
                        return new ValidationResult("Oops birth date is not valid, please try again");

                    return ValidationResult.Success;
                }).Select(birhDate => DateTime.Parse(birhDate));
            }
        }
    }
}
