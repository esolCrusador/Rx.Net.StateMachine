using Rx.Net.StateMachine.Persistance;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Collections.Generic;
using Rx.Net.StateMachine.Tests.Extensions;
using Rx.Net.StateMachine.Tests.Events;
using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Tests.DataAccess;
using System.Reactive;
using Rx.Net.StateMachine.Tests.Awaiters;
using Rx.Net.StateMachine.Extensions;
using Rx.Net.StateMachine.Flow;

namespace Rx.Net.StateMachine.Tests.Controls
{
    public class RequestCommentControl : IControl
    {
        private readonly WorkflowManagerAccessor<UserContext> _workflowManagerAccessor;
        private readonly TaskRepository _taskRepository;
        private readonly ChatFake _chat;
        private readonly FakeScheduler _scheduler;
        private readonly ShowCommentControl _showCommentControl;

        public RequestCommentControl(WorkflowManagerAccessor<UserContext> workflowManagerAccessor, TaskRepository taskRepository, ChatFake chat, FakeScheduler scheduler, ShowCommentControl showCommentControl)
        {
            _workflowManagerAccessor = workflowManagerAccessor;
            _taskRepository = taskRepository;
            _chat = chat;
            _scheduler = scheduler;
            _showCommentControl = showCommentControl;
        }

        public IFlow<Unit> StartDialog(StateMachineScope scope, TaskMessageContext source, bool mondatory)
        {
            var userContext = scope.GetContext<UserContext>();

            return scope.StartFlow(async () =>
            {
                await _workflowManagerAccessor.WorkflowManager.RemoveDefaultSesssions(scope.SessionId, userContext.ContextId.ToString());
                await scope.MakeDefault(true);

                return await _chat.SendBotMessage(userContext.BotId, userContext.ChatId, "Please send message to this chat");
            })
            .PersistDisposableItem()
            .Persist("CommentRequested")
            .WhenAny(
                "UserMessageOrTimeout",
                inner =>
                    inner.StopAndWait().For<BotFrameworkMessage>("UserMessage", _ => new BotFrameworkMessageAwaiter(userContext))
                    .SelectAsync(async (message, innerScope) =>
                    {
                        var comment = await _taskRepository.AddComment(
                            source.TaskId,
                            userContext.UserId,
                            message.Text,
                            new Dictionary<string, string> { ["SessionId"] = innerScope.SessionId.ToString("n") }
                        );

                        await _chat.DeleteUserMessage(userContext.BotId, userContext.ChatId, message.MessageId);
                        await _showCommentControl.ShowComment(userContext, comment, source.MessageId);
                    }),
                inner =>
                {
                    if (mondatory)
                        return null;

                    return inner.SelectAsync(async (_, innerScope) =>
                                        {
                                            var timeout = new TimeoutEvent
                                            {
                                                EventId = Guid.NewGuid(),
                                                SessionId = innerScope.SessionId
                                            };
                                            await _scheduler.ScheduleEvent(timeout, TimeSpan.FromMinutes(5));
                                            return timeout.EventId;
                                        })
                                        .Persist("TimeoutAdded")
                                        .StopAndWait().For<TimeoutEvent>("Timeout", to => new TimeoutEventAwaiter(to))
                                        .MapToVoid();
                },
                inner => inner.StopAndWait().For<DefaultSessionRemoved>("DefaultSessionRemoved", DefaultSessionRemovedAwaiter.Default)
                    .MapToVoid()
            )
            .FinallyAsync(async (isExecuted, el, ex) =>
            {
                if (isExecuted)
                    await scope.MakeDefault(false);
            })
            .DeleteMssages(_chat);
        }
    }
}
