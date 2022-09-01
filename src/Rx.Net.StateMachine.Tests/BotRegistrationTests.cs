using FluentAssertions;
using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly WorkflowManager _workflowManager;
        private readonly IDisposable _messagesHandling;
        private readonly BotFake _botFake;

        private BehaviorSubject<ConcurrentDictionary<Guid, HashSet<int>>> _handledMessages = new BehaviorSubject<ConcurrentDictionary<Guid, HashSet<int>>>(new ConcurrentDictionary<Guid, HashSet<int>>());

        public BotRegistrationTests()
        {
            _botFake = new BotFake();
            _dataStore = new SessionStateDataStore();
            _workflowManager = new WorkflowManager(
                new JsonSerializerOptions(),
                () => new SessionStateUnitOfWork(_dataStore),
                ss => scope => GetRegistrationWorkflow(scope)
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
            await _workflowManager.HandleEvent(message, new UserContext { UserId = message.UserId });

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
        private IObservable<Unit> GetRegistrationWorkflow(StateMachineScope scope)
        {
            var ctx = scope.GetContext<UserContext>();
            return Observable.FromAsync(() => _botFake.SendBotMessage(ctx.UserId, "Hello, please follow steps to pass registration process"))
                .Select(_ => new UserModel { Id = ctx.UserId })
                .Persist(scope, "UserId")
                .Select(user => GetFirstName(scope.BeginScope("FirstName")).Select(firstName =>
                {
                    user.FirstName = firstName;
                    return user;
                }))
                .Concat()
                .Persist(scope, "FirstName")
                .Select(user => GetLastName(scope.BeginScope("LastName")).Select(lastName =>
                {
                    user.LastName = lastName;
                    return user;
                }))
                .Concat()
                .Persist(scope, "LastName")
                .Select(user => GetBirthDate(scope.BeginScope("BirthDate")).Select(birthDate =>
                {
                    user.BirthDate = birthDate;
                    return user;
                }))
                .Concat()
                .SelectAsync(async user =>
                {
                    await _botFake.SendBotMessage(ctx.UserId, $"You was successfuly registered: {JsonSerializer.Serialize(user)}");

                    return Unit.Default;
                })
                .Concat();
        }

        private IObservable<string> GetFirstName(StateMachineScope scope, int attempt = 1)
        {
            var ctx = scope.GetContext<UserContext>();
            return Observable.FromAsync(() => _botFake.SendBotMessage(ctx.UserId, "Please enter your first name"))
                .Persist(scope, $"AskUserFirstName-{attempt}")
                .StopAndWait().For<BotFrameworkMessage>(scope, $"MessageReceived-{attempt}")
                .Select(message =>
                {
                    if (!string.IsNullOrWhiteSpace(message.Text))
                        return StateMachineObservableExtensions.Of(message.Text);

                    return Observable.FromAsync(() => _botFake.SendBotMessage(ctx.UserId, "Oops first name is not valid, please try again"))
                                .Persist(scope, $"InvalidFirstName-{attempt}")
                                .Select(_ => GetFirstName(scope, attempt + 1))
                                .Concat();
                }).Concat();
        }

        private IObservable<string> GetLastName(StateMachineScope scope, int attempt = 1)
        {
            var ctx = scope.GetContext<UserContext>();
            return Observable.FromAsync(() => _botFake.SendBotMessage(ctx.UserId, "Please enter your last name"))
                .Persist(scope, $"AskUserLastName-{attempt}")
                .StopAndWait().For<BotFrameworkMessage>(scope, $"MessageReceived-{attempt}")
                .Select(message =>
                {
                    if (!string.IsNullOrWhiteSpace(message.Text))
                        return StateMachineObservableExtensions.Of(message.Text);

                    return Observable.FromAsync(() => _botFake.SendBotMessage(ctx.UserId, "Oops last name is not valid, please try again"))
                                .Persist(scope, $"InvalidLastName-{attempt}")
                                .Select(_ => GetLastName(scope, attempt + 1))
                                .Concat();
                }).Concat();
        }

        private IObservable<DateTime> GetBirthDate(StateMachineScope scope, int attempt = 1)
        {
            var ctx = scope.GetContext<UserContext>();
            return Observable.FromAsync(() => _botFake.SendBotMessage(ctx.UserId, "Please enter your birth date"))
                .Persist(scope, $"AskUserBirthDate-{attempt}")
                .StopAndWait().For<BotFrameworkMessage>(scope, $"MessageReceived-{attempt}")
                .Select(message =>
                {
                    if (!string.IsNullOrWhiteSpace(message.Text) && DateTime.TryParse(message.Text, out var birthDate))
                        return StateMachineObservableExtensions.Of(birthDate);

                    return Observable.FromAsync(() => _botFake.SendBotMessage(ctx.UserId, "Oops birth date is not valid, please try again"))
                                .SelectAsync(_ => _botFake.SendBotMessage(ctx.UserId, $"For example: {DateTime.Now.AddYears(-30).ToShortDateString()}"))
                                .Persist(scope, $"InvalidBirthDate-{attempt}")
                                .Select(_ => GetBirthDate(scope, attempt + 1))
                                .Concat();
                }).Concat();
        }
    }
}
