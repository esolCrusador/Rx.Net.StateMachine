using Rx.Net.StateMachine.Persistance;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Models;
using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.Tests.Extensions;
using Rx.Net.StateMachine.Tests.Events;
using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Tests.DataAccess;
using System.Reactive;
using Rx.Net.StateMachine.Tests.Awaiters;
using Rx.Net.StateMachine.Extensions;

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

        public IObservable<Unit> StartDialog(StateMachineScope scope, TaskMessageContext source, bool mondatory)
        {
            var userContext = scope.GetContext<UserContext>();
            
            return Observable.FromAsync(async () =>
            {
                await _workflowManagerAccessor.WorkflowManager.RemoveDefaultSesssions(scope.SessionId, userContext.ContextId.ToString());
                await scope.MakeDefault(true);

                return await _chat.SendBotMessage(userContext.BotId, userContext.ChatId, "Please send message to this chat");
            })
            .PersistDisposableItem(scope)
            .Persist(scope, "CommentRequested")
            .WhenAny(
                scope,
                "UserMessageOrTimeout",
                (messageId, innerScope) =>
                    innerScope.StopAndWait<BotFrameworkMessage>("UserMessage", new BotFrameworkMessageAwaiter(userContext))
                    .SelectAsync(async message =>
                    {
                        var comment = await _taskRepository.AddComment(
                            source.TaskId,
                            userContext.UserId,
                            message.Text,
                            new Dictionary<string, string> { ["SessionId"] = innerScope.SessionId.ToString("n") }
                        );

                        await _chat.DeleteUserMessage(userContext.BotId, userContext.ChatId, message.MessageId);
                        await _showCommentControl.ShowComment(userContext, comment, source.MessageId);
                    })
                    .Concat(),
                (messageId, innerScope) => {
                    if (mondatory)
                        return null;

                    return Observable.FromAsync(async () =>
                                        {
                                            var timeout = new TimeoutEvent
                                            {
                                                EventId = Guid.NewGuid(),
                                                SessionId = innerScope.SessionId
                                            };
                                            await _scheduler.ScheduleEvent(timeout, TimeSpan.FromMinutes(5));
                                            return timeout.EventId;
                                        })
                                        .Persist(scope, "TimeoutAdded")
                                        .StopAndWait().For<TimeoutEvent>(innerScope, "Timeout", to => new TimeoutEventAwaiter(to))
                                        .MapToVoid();
                },
                (messageId, innerScope) =>
                    innerScope.StopAndWait<DefaultSessionRemoved>("DefaultSessionRemoved", DefaultSessionRemovedAwaiter.Default)
                    .MapToVoid()
            )
            .FinallyAsync(async (isExecuted, el, ex) =>
            {
                if (isExecuted)
                    await scope.MakeDefault(false);
            })
            .DeleteMssages(scope, _chat)
            .Take(1);
        }
    }
}
