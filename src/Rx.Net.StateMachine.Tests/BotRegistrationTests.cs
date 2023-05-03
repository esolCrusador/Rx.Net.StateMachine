using FluentAssertions;
using FluentAssertions.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rx.Net.StateMachine.EntityFramework;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.Persistance;
using Rx.Net.StateMachine.Tests.Extensions;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using Rx.Net.StateMachine.Tests.Repositories;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
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
    public abstract class BotRegistrationTests : IAsyncLifetime
    {
        private readonly long _botId = new Random().NextInt64(long.MaxValue);
        private readonly ServiceProvider _services;
        private WorkflowManager<UserContext> _workflowManager => _services.GetRequiredService<WorkflowManager<UserContext>>();
        private ChatFake _chatFake => _services.GetRequiredService<ChatFake>();
        private SessionStateDbContextFactory<TestSessionStateDbContext, UserContext, int> _createContextFactory =>
            _services.GetRequiredService<SessionStateDbContextFactory<TestSessionStateDbContext, UserContext, int>>();
        private UserContextRepository UserContextRepository => _services.GetRequiredService<UserContextRepository>();

        private BotRegistrationTests(Func<TestSessionStateDbContext> createContext)
        {
            var services = new ServiceCollection();
            services.AddSingleton(createContext);
            services.AddStateMachine<UserContext>();
            services.AddWorkflowFactory<BotRegistrationWorkflowFactory>();
            services.AddSingleton<ChatFake>();
            services.AddEFStateMachine()
                .WithContext<UserContext>()
                .WithKey(uc => uc.ContextId)
                .WithDbContext(createContext)
                .WithUnitOfWork<TestEFSessionStateUnitOfWork>();
            services.AddSingleton<UserContextRepository>();
            services.AddSingleton(sp => sp.GetRequiredService<ChatFake>().AddMessageHandler(HandleUserMessage));

            _services = services.BuildServiceProvider();
            _services.GetServices<HandlerRegistration>();
        }

        [Trait("Category", "Fast")]
        public class Fast : BotRegistrationTests
        {
            public Fast() : this($"TestDatabase-{Guid.NewGuid()}")
            {
            }

            private Fast(string databaseName) :
                base(() => new TestSessionStateDbContext(new DbContextOptionsBuilder().UseInMemoryDatabase(databaseName).Options))
            {
            }
        }

        [Trait("Category", "Slow")]
        public class Slow : BotRegistrationTests
        {
            public Slow() : this($"TestDatabase-{Guid.NewGuid()}")
            {

            }

            private Slow(string databaseName) :
                base(() => new TestSessionStateDbContext(new DbContextOptionsBuilder()
                    .UseSqlServer("Data Source =.; Integrated Security = True; TrustServerCertificate=True; Initial Catalog=TestDatabase;".Replace("TestDatabase", databaseName))
                    .Options))
            {

            }
        }

        public async Task InitializeAsync()
        {
            await using var context = _createContextFactory.Create();
            await context.Database.EnsureCreatedAsync();
        }

        public async Task DisposeAsync()
        {
            await using var context = _createContextFactory.Create();
            await context.Database.EnsureDeletedAsync();

            await _services.DisposeAsync();
        }

        [Fact]
        public async Task Should_Return_User_Information()
        {
            var chatId = new Random().NextInt64(long.MaxValue);
            await _chatFake.SendUserMessage(_botId, chatId, "/Start");

            var botMessages = _chatFake.ReadNewBotMessageTexts(_botId, chatId);
            botMessages.Should().BeEquivalentTo("Hello, please follow steps to pass registration process", "Please enter your first name");

            await _chatFake.SendUserMessage(_botId, chatId, "Boris");
            botMessages = _chatFake.ReadNewBotMessageTexts(_botId, chatId);
            botMessages.Should().BeEquivalentTo("Please enter your last name");

            await _chatFake.SendUserMessage(_botId, chatId, "Sotsky");
            botMessages = _chatFake.ReadNewBotMessageTexts(_botId, chatId);
            botMessages.Should().BeEquivalentTo("Please enter your birth date");

            await _chatFake.SendUserMessage(_botId, chatId, new DateTime(1987, 6, 23).ToShortDateString());
            botMessages = _chatFake.ReadNewBotMessageTexts(_botId, chatId);
            var lastMessage = botMessages.Single();
            var user = await UserContextRepository.GetUserContext(_botId, chatId);
            lastMessage.Should().Contain(user.UserId.ToString());
            lastMessage.Should().Contain("Boris");
            lastMessage.Should().Contain("Sotsky");
            lastMessage.Should().Contain("1987");

            var allMessages = _chatFake.ReadAllMessageTexts(_botId, chatId);
            allMessages.Count.Should().Be(2);
            allMessages.First().Should().Be("/Start");
            allMessages.Last().Should().ContainAll("You was successfuly registered", "Boris", "Sotsky", "1987");
        }

        [Fact]
        public async Task Should_Ask_To_Reenter_FirstName_If_Not_Valid()
        {
            var boris = new Random().NextInt64(long.MaxValue);
            await _chatFake.SendUserMessage(_botId, boris, "/Start");

            var botMessages = _chatFake.ReadNewBotMessageTexts(_botId, boris);
            botMessages.Should().BeEquivalentTo("Hello, please follow steps to pass registration process", "Please enter your first name");

            await _chatFake.SendUserMessage(_botId, boris, "   ");
            botMessages = _chatFake.ReadNewBotMessageTexts(_botId, boris);
            botMessages.Should().BeEquivalentTo("Oops first name is not valid, please try again", "Please enter your first name");

            await _chatFake.SendUserMessage(_botId, boris, " ");
            botMessages = _chatFake.ReadNewBotMessageTexts(_botId, boris);
            botMessages.Should().BeEquivalentTo("Oops first name is not valid, please try again", "Please enter your first name");

            await _chatFake.SendUserMessage(_botId, boris, "Boris");
            botMessages = _chatFake.ReadNewBotMessageTexts(_botId, boris);
            botMessages.Should().BeEquivalentTo("Please enter your last name");

            var allMessages = _chatFake.ReadAllMessageTexts(_botId, boris);
            allMessages.Count.Should().Be(3);
            allMessages.First().Should().Be("/Start");
            allMessages.Skip(1).First().Should().Contain("Hello");
            allMessages.Last().Should().Be("Please enter your last name");
        }

        private async Task HandleUserMessage(BotFrameworkMessage message)
        {
            var userContext = await UserContextRepository.GetUserContext(message.BotId, message.ChatId);

            if (string.Equals(message.Text, "/start", StringComparison.OrdinalIgnoreCase))
                await _workflowManager.StartHandle(BotRegistrationWorkflowFactory.Id, userContext);
            else
                await _workflowManager.HandleEvent(message);
        }

        // Workflow: https://www.figma.com/file/WPqeeRL8EjiH1rzXT1os7o/User-Registration-Case?node-id=0%3A1
        class BotRegistrationWorkflowFactory : WorkflowFactory<UserModel>
        {
            ChatFake _botFake;

            public const string Id = "bot-registration";
            public override string WorkflowId => Id;

            public BotRegistrationWorkflowFactory(ChatFake botFake) => _botFake = botFake;

            public override IObservable<UserModel> GetResult(StateMachineScope scope)
            {
                var ctx = scope.GetContext<UserContext>();
                return Observable.FromAsync(() => _botFake.SendBotMessage(ctx.BotId, ctx.ChatId, "Hello, please follow steps to pass registration process"))
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
                    .DeleteMssages(scope, _botFake);
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
                                    .Select(_ => GetFirstName(scope))
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
