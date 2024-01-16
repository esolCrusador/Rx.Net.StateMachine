using FluentAssertions;
using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Extensions;
using Rx.Net.StateMachine.Flow;
using Rx.Net.StateMachine.Tests.DataAccess;
using Rx.Net.StateMachine.Tests.Testing;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Xunit;

namespace Rx.Net.StateMachine.Tests
{
    public abstract class ErrorHandlingTests : IAsyncLifetime
    {
        private readonly StateMachineTestContext _ctx;
        private Func<MessageHandled, Task>? _handleResult;

        private ErrorHandlingTests(StateMachineTestContextBuilder builder)
        {
            builder.AddEventHandler<BroadcastMessage>(HandleEvent<BroadcastMessage>);
            builder.AddEventHandler<MessageHandled>(HandleResult);
            builder.AddWorkflow<BroadcastSubscriberWorkflow>().AddWorkflow<BroadcastMultipleMessagesWorkflow>().AddWorkflow<HandleBroadcastedMessageWorkflow>();

            builder.ForContextBuilder()
                .AddAwaiterHandler<BroadcastMessage>(b => b.WithAwaiter<BroadcastMessageAwaiter>())
                .AddAwaiterHandler<MessageHandled>(b => b.WithAwaiter<MessageHandledAwaiter>());


            _ctx = builder.Build();
        }

        [Trait("Category", "Fast")]
        public class Fast : ErrorHandlingTests
        {
            public Fast() : base(StateMachineTestContextBuilder.Fast())
            {
            }
        }

        [Trait("Category", "Slow")]
        public class Slow : ErrorHandlingTests
        {
            public Slow() : base(StateMachineTestContextBuilder.Slow())
            {
            }

            [Fact]
            public async Task Should_Ignore_Concurrency_On_Messages_Between_Same_Session()
            {
                var messageId = Guid.NewGuid();

                var userContext = await _ctx.UserContextRepository.GetUserOrCreateContext(1, 1, "1", "1");

                await _ctx.WorkflowManager.Start(userContext, messageId).Workflow<HandleBroadcastedMessageWorkflow>();

                int resultTriggeredTimes = 0;
                _handleResult = r =>
                {
                    resultTriggeredTimes++;
                    return Task.CompletedTask;
                };

                await _ctx.MessageQueue.SendAndWait(new BroadcastMessage { MessageId = messageId });
                resultTriggeredTimes.Should().Be(1);
            }
        }

        public async Task InitializeAsync()
        {
            await _ctx.InititalizeAsync();
        }

        public async Task DisposeAsync()
        {
            await _ctx.DisposeAsync();
        }

        private Task HandleEvent<TEvent>(TEvent ev)
            where TEvent : class
        {
            return _ctx.WorkflowManager.HandleEvent(ev);
        }

        [Fact]
        public async Task Should_Throw_Handling_Error()
        {
            var messageId = Guid.NewGuid();
            var userContext = await _ctx.UserContextRepository.GetUserOrCreateContext(1, 1, "1", "1");
            await _ctx.WorkflowManager.Start<Guid>(userContext, messageId).Workflow<BroadcastSubscriberWorkflow>();

            _handleResult = _ => throw new Exception("Random");

            Func<Task> eventOne = () => _ctx.MessageQueue.SendAndWait(new BroadcastMessage { MessageId = messageId });
            await eventOne.Should().ThrowAsync<Exception>().WithMessage("Random");
        }

        [Fact]
        public async Task Should_Retry_On_Failure()
        {
            var messageId = Guid.NewGuid();
            var userContext = await _ctx.UserContextRepository.GetUserOrCreateContext(1, 1, "1", "1");
            await _ctx.WorkflowManager.Start<Guid>(userContext, messageId).Workflow<BroadcastSubscriberWorkflow>();

            _handleResult = _ =>
            {
                throw new Exception("Random");
            };

            try
            {
                await _ctx.MessageQueue.SendAndWait(new BroadcastMessage { MessageId = messageId });
            }
            catch (Exception)
            {
            }

            var handled = new TaskCompletionSource<bool>(default);
            _handleResult = _ =>
            {
                handled.TrySetResult(true);
                return Task.CompletedTask;
            };
            await _ctx.MessageQueue.SendAndWait(new BroadcastMessage { MessageId = messageId });

            (await handled.Task).Should().BeTrue();
        }

        [Fact]
        public async Task Should_Finish_Correctly_Handled_Workflow_If_Multiple_Handled()
        {
            var messageId = Guid.NewGuid();
            var userContext = await _ctx.UserContextRepository.GetUserOrCreateContext(1, 1, "1", "1");

            var wf1 = await _ctx.WorkflowManager.Start<Guid>(userContext, messageId).Workflow<BroadcastSubscriberWorkflow>();
            var wf2 = await _ctx.WorkflowManager.Start<Guid>(userContext, messageId).Workflow<BroadcastSubscriberWorkflow>();

            int wf1Handled = 0;
            int wf2Handled = 0;
            int wf2Failed = 0;

            _handleResult = async r =>
            {
                if (r.SessionId == wf1.SessionId)
                {
                    await Task.Delay(100);
                    wf1Handled++;
                }
                else
                {
                    if (wf2Failed == 0)
                    {
                        wf2Failed++;
                        throw new Exception("State Exception");
                    }
                    else
                        wf2Handled++;
                }
            };

            try
            {
                await _ctx.MessageQueue.SendAndWait(new BroadcastMessage { MessageId = messageId });
            }
            catch (Exception)
            {
            }
            await _ctx.MessageQueue.SendAndWait(new BroadcastMessage { MessageId = messageId });

            wf2Handled.Should().Be(1);
            wf2Failed.Should().Be(1);
            wf1Handled.Should().Be(1);
        }

        [Fact]
        public async Task Should_Not_Repeate_Failed_Action_On_Concurrency()
        {
            var message1Id = Guid.NewGuid();
            var message2Id = Guid.NewGuid();

            var userContext = await _ctx.UserContextRepository.GetUserOrCreateContext(1, 1, "1", "1");

            var wf1 = await _ctx.WorkflowManager.Start(userContext, new[] { message1Id, message2Id }).Workflow<BroadcastMultipleMessagesWorkflow>();
            var wf2 = await _ctx.WorkflowManager.Start(userContext, new[] { message1Id }).Workflow<BroadcastMultipleMessagesWorkflow>();

            int wf1Handled = 0;
            int wf2Failed = 0;

            _handleResult = async r =>
            {
                if (r.SessionId == wf1.SessionId)
                {
                    await Task.Delay(100);
                    wf1Handled++;
                }
                else
                {
                    wf2Failed++;
                    throw new Exception("State Exception");
                }
            };

            _ctx.GlobalContextState.OnBeforeNextSaveChanges(() => _ctx.MessageQueue.SendAndWait(new BroadcastMessage { MessageId = message2Id }));
            try
            {
                await _ctx.MessageQueue.SendAndWait(new BroadcastMessage { MessageId = message1Id });
            }
            catch (Exception)
            {
            }
            try
            {
                await _ctx.MessageQueue.SendAndWait(new BroadcastMessage { MessageId = message1Id });
            }
            catch (Exception)
            {
            }

            wf1Handled.Should().Be(4); // 1st time, concurrency, concurrency retry, 2nd time.
            wf2Failed.Should().Be(3); // 1st time, concurrency retry of first session, 2nd time.
        }

        private Task HandleResult(MessageHandled broadcastMessage)
        {
            return Task.WhenAll(HandleEvent(broadcastMessage), _handleResult.GetValue(nameof(_handleResult))(broadcastMessage));
        }

        class BroadcastSubscriberWorkflow : Workflow<Guid>
        {
            private readonly MessageQueue _messageQueue;

            public override string WorkflowId => nameof(BroadcastSubscriberWorkflow);

            public BroadcastSubscriberWorkflow(MessageQueue messageQueue)
            {
                _messageQueue = messageQueue;
            }

            public override IFlow<Unit> Execute(IFlow<Guid> flow)
            {
                return flow.Persist("MessageId")
                    .StopAndWait().For<BroadcastMessage>("BroadcastMessage", messageId => new BroadcastMessageAwaiter(messageId))
                    .SelectAsync((ev, scope) => _messageQueue.SendAndWait(
                        new MessageHandled { MessageId = ev.MessageId, SessionId = scope.SessionId, Version = scope.Version }
                    )
                );
            }
        }

        class BroadcastMultipleMessagesWorkflow : Workflow<Guid[]>
        {
            private readonly MessageQueue _messageQueue;

            public override string WorkflowId => nameof(BroadcastMultipleMessagesWorkflow);

            public BroadcastMultipleMessagesWorkflow(MessageQueue messageQueue)
            {
                _messageQueue = messageQueue;
            }

            public override IFlow<Unit> Execute(IFlow<Guid[]> flow)
            {
                return flow.Persist("MessageId")
                    .WhenAll((messageIds, scope) =>
                        messageIds.Select(messageId =>
                            scope.BeginScope(messageId.ToString("n"))
                            .StopAndWait<BroadcastMessage>("BroadcastMessage", new BroadcastMessageAwaiter(messageId))
                            .SelectAsync((ev, scope) => _messageQueue.SendAndWait(
                                new MessageHandled { MessageId = ev.MessageId, SessionId = scope.SessionId, Version = scope.Version }
                            ))
                    )
                ).MapToVoid();
            }
        }

        class HandleBroadcastedMessageWorkflow : Workflow<Guid>
        {
            private readonly MessageQueue _messageQueue;

            public HandleBroadcastedMessageWorkflow(MessageQueue messageQueue)
            {
                _messageQueue = messageQueue;
            }

            public override string WorkflowId => nameof(HandleBroadcastedMessageWorkflow);


            public override IFlow<Unit> Execute(IFlow<Guid> flow)
            {
                return flow.Persist("MessageId")
                    .WhenAll(
                        childFlow => childFlow.Select((messageId, scope) =>
                            scope.StopAndWait<BroadcastMessage>("BroadcastMessage", new BroadcastMessageAwaiter(messageId))
                            .SelectAsync(async (ev, scope) =>
                            {
                                await _messageQueue.Send(
                                    new MessageHandled { MessageId = ev.MessageId, SessionId = scope.SessionId, Version = scope.Version }
                                );
                                await Task.Delay(100);
                            })
                            .Persist("BroadcastedMessage")
                        ).MapToVoid(),
                        childFlow => childFlow.Select((messageId, scope) =>
                            scope.StopAndWait<MessageHandled>("MessageHandled", new MessageHandledAwaiter(messageId))
                        ).MapToVoid()
                    ).MapToVoid();
            }
        }

        class MessageHandled : IIgnoreSessionVersion
        {
            public required Guid SessionId { get; set; }
            public required Guid MessageId { get; set; }
            public required int Version { get; set; }
        }

        class BroadcastMessage
        {
            public required Guid MessageId { get; set; }
        }

        class BroadcastMessageAwaiter : IEventAwaiter<BroadcastMessage>
        {
            public string AwaiterId => $"{nameof(BroadcastMessage)}-{MessageId:n}";

            public Guid MessageId { get; }

            public BroadcastMessageAwaiter(BroadcastMessage broadcastMessage) : this(broadcastMessage.MessageId)
            {
            }

            public BroadcastMessageAwaiter(Guid messageId)
            {
                MessageId = messageId;
            }
        }

        class MessageHandledAwaiter : IEventAwaiter<MessageHandled>
        {
            public string AwaiterId => $"{nameof(MessageHandled)}-{MessageId:n}";

            public Guid MessageId { get; }

            public MessageHandledAwaiter(MessageHandled messageHandled) : this(messageHandled.MessageId)
            {
            }

            public MessageHandledAwaiter(Guid messageId)
            {
                MessageId = messageId;
            }
        }
    }
}
