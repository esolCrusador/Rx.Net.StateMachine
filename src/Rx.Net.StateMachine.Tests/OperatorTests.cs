using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using Rx.Net.StateMachine.Tests.Testing;
using Rx.Net.StateMachine.WorkflowFactories;
using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.Extensions;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;
using System.Linq;
using FluentAssertions;
using Rx.Net.StateMachine.Tests.Events;
using Rx.Net.StateMachine.EntityFramework.Tables;
using Microsoft.EntityFrameworkCore;
using Rx.Net.StateMachine.Tests.Awaiters;

namespace Rx.Net.StateMachine.Tests
{
    public abstract class OperatorTests : IAsyncLifetime
    {
        private readonly long _botId = new Random().NextInt64(long.MaxValue);
        private long _userId;
        private readonly StateMachineTestContext _ctx;

        public OperatorTests(StateMachineTestContextBuilder builder)
        {
            _ctx = builder
                .AddWorkflow<SampleWorkflow>()
                .AddMessageHandler(HandleMessage)
                .AddClickHandler(HandleClick)
                .AddEventHandler<TimeoutEvent>(HandleEvent)
                .Build();
        }

        public async Task InitializeAsync()
        {
            await _ctx.InititalizeAsync();
            _userId = await _ctx.Chat.RegisterUser(new UserInfo
            {
                FirstName = "Boris",
                LastName = "Sotsky",
                Username = "esolCrusador"
            });
        }
        public async Task DisposeAsync()
        {
            await _ctx.DisposeAsync();
        }

        [Trait("Category", "Fast")]
        public class Fast : OperatorTests
        {
            public Fast() : base(StateMachineTestContextBuilder.Fast())
            {
            }
        }

        [Trait("Category", "Slow")]
        public class Slow : OperatorTests
        {
            public Slow() : base(StateMachineTestContextBuilder.Slow())
            {
            }
        }

        [Fact]
        public async Task Should_Show_Final_Message_By_Timeout()
        {
            await _ctx.Chat.SendUserMessage(_botId, _userId, "/start");
            var message = _ctx.Chat.ReadNewBotMessages(_botId, _userId).Single();
            message.Text.Should().Be("Hi");

            await _ctx.Scheduler.RewindTime(TimeSpan.FromSeconds(35));

            message = _ctx.Chat.ReadNewBotMessages(_botId, _userId).Single();
            message.Text.Should().Be("Well Done!");

            await using var context = _ctx.ContextFactory.Create();
            var ss = await context.Set<SessionStateTable<UserContext, int>>().Select(ss => new
            {
                ss.SessionStateId,
                Awaiters = ss.Awaiters.ToList()
            }).FirstAsync();
            ss.Awaiters.Should().BeEmpty();
        }

        private async Task HandleMessage(BotFrameworkMessage message)
        {
            var userContext = await _ctx.UserContextRepository.GetUserOrCreateContext(message.BotId, message.ChatId,
                message.UserInfo.Name,
                message.UserInfo.Username ?? message.UserInfo.UserId.ToString()
            );

            if (string.Equals(message.Text, "/start", StringComparison.OrdinalIgnoreCase))
                await _ctx.WorkflowManager.Start(userContext).Workflow<SampleWorkflow>();
            else
                throw new NotSupportedException();
        }

        private async Task HandleClick(BotFrameworkButtonClick buttonClick)
        {
            await _ctx.WorkflowManager.HandleEvent(buttonClick);
        }

        private async Task HandleEvent<TEvent>(TEvent ev)
        {
            await _ctx.WorkflowManager.HandleEvent(ev);
        }

        class SampleWorkflow : Workflow
        {
            public const string Id = nameof(SampleWorkflow);
            private readonly ChatFake _chat;
            private readonly FakeScheduler _scheduler;

            public override string WorkflowId => Id;

            public SampleWorkflow(ChatFake chat, FakeScheduler scheduler)
            {
                _chat = chat;
                _scheduler = scheduler;
            }

            public override IObservable<Unit> Execute(StateMachineScope scope)
            {
                var userContext = scope.GetContext<UserContext>();
                return Observable.FromAsync(() => _chat.SendButtonsBotMessage(
                    userContext.BotId,
                    userContext.ChatId,
                    "Hi",
                    new KeyValuePair<string, string>(new WorkflowCallbackQuery { SessionId = scope.SessionState.SessionStateId, Command = "next" }.ToString(), "Hi")
                    ))
                    .Persist(scope, "HiMessage")
                    .WhenAny(
                        scope,
                        "HiAwaiter",
                        (messageId, anyScope) => Observable.FromAsync(async () =>
                        {
                            var ev = new TimeoutEvent
                            {
                                SessionId = anyScope.SessionState.SessionStateId.GetValue("SessionStateId"),
                                EventId = Guid.NewGuid()
                            };
                            await _scheduler.ScheduleEvent(ev, TimeSpan.FromSeconds(30));

                            return ev.EventId;
                        }).Persist(anyScope, "Timeout1").StopAndWait().For<TimeoutEvent>(anyScope, "FirstTimeoutEvent", eventId => new TimeoutEventAwaiter(eventId)).MapToVoid(),
                       (messageId, anyScope) => anyScope.StopAndWait<BotFrameworkButtonClick>("HiButton", new BotFrameworkButtonClickAwaiter(userContext, messageId)).MapToVoid()
                    )
                    .SelectAsync(() => _chat.SendBotMessage(userContext.BotId, userContext.ChatId, "Well Done!"))
                    .Concat()
                    .MapToVoid();
            }
        }


    }
}
