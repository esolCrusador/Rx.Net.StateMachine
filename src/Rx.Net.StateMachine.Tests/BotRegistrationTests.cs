using FluentAssertions;
using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.Tests.Extensions;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using Rx.Net.StateMachine.Tests.Testing;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reactive.Linq;
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
    public abstract class BotRegistrationTests : IAsyncLifetime
    {
        private readonly long _botId = new Random().NextInt64(long.MaxValue);
        private StateMachineTestContext _ctx;

        private BotRegistrationTests(StateMachineTestContextBuilder builder)
        {
            builder.AddWorkflow<BotRegistrationWorkflowFactory>();
            builder.AddMessageHandler(HandleUserMessage);

            _ctx = builder.Build();
        }

        [Trait("Category", "Fast")]
        public class Fast : BotRegistrationTests
        {
            public Fast() : base(StateMachineTestContextBuilder.Fast())
            {
            }
        }

        [Trait("Category", "Slow")]
        public class Slow : BotRegistrationTests
        {
            public Slow() : base(StateMachineTestContextBuilder.Slow())
            {
            }
        }

        public Task InitializeAsync()
        {
            return _ctx.InititalizeAsync();
        }

        public async Task DisposeAsync()
        {
            await _ctx.DisposeAsync();
        }

        [Fact]
        public async Task Should_Return_User_Information()
        {
            var chatId = await _ctx.Chat.RegisterUser(new UserInfo
            {
                FirstName = "Boris",
                LastName = "Sotsky",
                Username = "esolCrusador"
            });
            await _ctx.Chat.SendUserMessage(_botId, chatId, "/Start");

            var botMessages = _ctx.Chat.ReadNewBotMessageTexts(_botId, chatId);
            botMessages.Should().BeEquivalentTo("Hello, please follow steps to pass registration process", "Please enter your first name");

            await _ctx.Chat.SendUserMessage(_botId, chatId, "Boris");
            botMessages = _ctx.Chat.ReadNewBotMessageTexts(_botId, chatId);
            botMessages.Should().BeEquivalentTo("Please enter your last name");

            await _ctx.Chat.SendUserMessage(_botId, chatId, "Sotsky");
            botMessages = _ctx.Chat.ReadNewBotMessageTexts(_botId, chatId);
            botMessages.Should().BeEquivalentTo("Please enter your birth date");

            await _ctx.Chat.SendUserMessage(_botId, chatId, new DateTime(1987, 6, 23).ToShortDateString());
            botMessages = _ctx.Chat.ReadNewBotMessageTexts(_botId, chatId);
            var lastMessage = botMessages.Single();
            var user = await _ctx.UserContextRepository.GetUserContext(_botId, chatId);
            lastMessage.Should().Contain(user.UserId.ToString());
            lastMessage.Should().Contain("Boris");
            lastMessage.Should().Contain("Sotsky");
            lastMessage.Should().Contain("1987");

            var allMessages = _ctx.Chat.ReadAllMessageTexts(_botId, chatId);
            allMessages.Count.Should().Be(2);
            allMessages.First().Should().Be("/Start");
            allMessages.Last().Should().ContainAll("You was successfuly registered", "Boris", "Sotsky", "1987");
        }

        [Fact]
        public async Task Should_Ask_To_Reenter_FirstName_If_Not_Valid()
        {
            var chatId = await _ctx.Chat.RegisterUser(new UserInfo
            {
                FirstName = "Boris",
                LastName = "Sotsky",
                Username = "esolCrusador"
            });
            await _ctx.Chat.SendUserMessage(_botId, chatId, "/Start");

            var botMessages = _ctx.Chat.ReadNewBotMessageTexts(_botId, chatId);
            botMessages.Should().BeEquivalentTo("Hello, please follow steps to pass registration process", "Please enter your first name");

            await _ctx.Chat.SendUserMessage(_botId, chatId, "   ");
            botMessages = _ctx.Chat.ReadNewBotMessageTexts(_botId, chatId);
            botMessages.Should().BeEquivalentTo("Oops first name is not valid, please try again", "Please enter your first name");

            await _ctx.Chat.SendUserMessage(_botId, chatId, " ");
            botMessages = _ctx.Chat.ReadNewBotMessageTexts(_botId, chatId);
            botMessages.Should().BeEquivalentTo("Oops first name is not valid, please try again", "Please enter your first name");

            await _ctx.Chat.SendUserMessage(_botId, chatId, "Boris");
            botMessages = _ctx.Chat.ReadNewBotMessageTexts(_botId, chatId);
            botMessages.Should().BeEquivalentTo("Please enter your last name");

            var allMessages = _ctx.Chat.ReadAllMessageTexts(_botId, chatId);
            allMessages.Count.Should().Be(3);
            allMessages.First().Should().Be("/Start");
            allMessages.Skip(1).First().Should().Contain("Hello");
            allMessages.Last().Should().Be("Please enter your last name");
        }

        private async Task HandleUserMessage(BotFrameworkMessage message)
        {
            var userContext = await _ctx.UserContextRepository.GetUserOrCreateContext(message.BotId, message.ChatId,
                message.UserInfo.Name,
                message.UserInfo.Username ?? message.UserInfo.UserId.ToString()
            );

            if (string.Equals(message.Text, "/start", StringComparison.OrdinalIgnoreCase))
                await _ctx.WorkflowManager.StartHandle(BotRegistrationWorkflowFactory.Id, userContext);
            else
                await _ctx.WorkflowManager.HandleEvent(message);
        }

        // Workflow: https://www.figma.com/file/WPqeeRL8EjiH1rzXT1os7o/User-Registration-Case?node-id=0%3A1
        class BotRegistrationWorkflowFactory : Workflow<UserModel>
        {
            ChatFake _botFake;

            public const string Id = "bot-registration";
            public override string WorkflowId => Id;

            public BotRegistrationWorkflowFactory(ChatFake botFake) => _botFake = botFake;

            public override IObservable<UserModel> GetResult(StateMachineScope scope)
            {
                var ctx = scope.GetContext<UserContext>();
                return Observable.FromAsync(async () =>
                {
                    await scope.MakeDefault(true);
                    return await _botFake.SendBotMessage(ctx.BotId, ctx.ChatId, "Hello, please follow steps to pass registration process");
                })
                    .PersistMessageId(scope)
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
                        await _botFake.SendBotMessage(ctx.BotId, ctx.ChatId, $"You was successfuly registered: {JsonSerializer.Serialize(user)}");

                        return user;
                    })
                    .Concat()
                    .DeleteMssages(scope, _botFake)
                    .FinallyAsync(async (isExecuted, el, ex) =>
                    {
                        if (isExecuted)
                            await scope.MakeDefault(false);
                    });
            }

            private IObservable<string> RequestStringInput(StateMachineScope scope, string displayName, string stateName, Func<string, ValidationResult> validate)
            {
                var ctx = scope.GetContext<UserContext>();
                return Observable.FromAsync(() => _botFake.SendBotMessage(ctx.BotId, ctx.ChatId, $"Please enter your {displayName}"))
                    .PersistMessageId(scope)
                    .Persist(scope, $"Ask{stateName}")
                    .StopAndWait().For<BotFrameworkMessage>(scope, "MessageReceived")
                    .PersistMessageId(scope)
                    .Select(message =>
                    {
                        string text = message.Text;
                        var validationResult = validate(text);
                        if (validationResult == ValidationResult.Success)
                            return StateMachineObservableExtensions.Of(message.Text);

                        return Observable.FromAsync(() => _botFake.SendBotMessage(ctx.BotId, ctx.ChatId, validationResult.ErrorMessage!))
                                    .PersistMessageId(scope)
                                    .Persist(scope, $"Invalid{stateName}")
                                    .IncreaseRecoursionDepth(scope)
                                    .Select(_ => RequestStringInput(scope, displayName, stateName, validate))
                                    .Concat();
                    }).Concat()
                    .DeleteMssages(scope, _botFake);
            }

            private IObservable<string> GetFirstName(StateMachineScope scope)
            {
                return RequestStringInput(scope, "first name", "FirstName", s =>
                {
                    if (string.IsNullOrWhiteSpace(s))
                        return new ValidationResult("Oops first name is not valid, please try again");

                    return ValidationResult.Success!;
                });
            }

            private IObservable<string> GetLastName(StateMachineScope scope)
            {
                return RequestStringInput(scope, "last name", "LastName", s =>
                {
                    if (string.IsNullOrWhiteSpace(s))
                        return new ValidationResult("Oops last name is not valid, please try again");

                    return ValidationResult.Success!;
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

                    return ValidationResult.Success!;
                }).Select(birhDate => DateTime.Parse(birhDate));
            }
        }
    }
}
