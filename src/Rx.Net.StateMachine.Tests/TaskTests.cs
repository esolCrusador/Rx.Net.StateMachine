using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Rx.Net.StateMachine.Persistance;
using Rx.Net.StateMachine.Tests.DataAccess;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Models;
using Rx.Net.StateMachine.Tests.Persistence;
using Rx.Net.StateMachine.Tests.Testing;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;
using Rx.Net.StateMachine.Tests.Events;
using Rx.Net.StateMachine.Tests.Controls;
using Rx.Net.StateMachine.Tests.Awaiters;
using Rx.Net.StateMachine.Flow;

namespace Rx.Net.StateMachine.Tests
{
    // https://www.figma.com/file/65UWsCMvohKGerVrUWWFdd/Task-Workflow?type=whiteboard&node-id=0-1&t=HtMQYV7kgZbTx2GO-0
    public abstract class TaskTests : IAsyncLifetime
    {
        private readonly StateMachineTestContext _ctx;
        private readonly long _botId = new Random().NextInt64(long.MaxValue);
        private long _curatorId;
        private long _studentId;

        private TaskTests(StateMachineTestContextBuilder builder)
        {
            builder.AddMessageHandler(HandleUserMessage)
                .AddClickHandler(HandleButtonClick)
                .AddEventHandler<TaskCreatedEvent>(HandleEvent)
                .AddEventHandler<TaskStateChanged>(HandleEvent)
                .AddEventHandler<TaskCommentAdded>(HandleEvent)
                .AddEventHandler<TimeoutEvent>(HandleEvent)
                .AddWorkflow<OnboardingWorkflow>()
                .AddWorkflow<CuratorWorkflow>()
                .AddWorkflow<TaskWorkflow>()
                .AddWorkflow<CuratorTaskWorkflow>();
            builder.Configure(s => s.AddSingleton<TaskRepository>());

            _ctx = builder.Build();
        }
        [Trait("Category", "Fast")]
        public class Fast : TaskTests
        {
            public Fast() : base(StateMachineTestContextBuilder.Fast())
            {
            }
        }
        [Trait("Category", "Slow")]
        public class Slow : TaskTests
        {
            public Slow() : base(StateMachineTestContextBuilder.Slow())
            {
            }
        }
        public async Task InitializeAsync()
        {
            await _ctx.InititalizeAsync();
            _curatorId = await _ctx.Chat.RegisterUser(new UserInfo
            {
                FirstName = "Veronica",
                LastName = "Sotskaya",
                Username = "veronica"
            });
            _studentId = await _ctx.Chat.RegisterUser(new UserInfo
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

        [Fact]
        public async Task Should_Send_Welcome_Message_To_Curator()
        {
            var messages = await AddCurator();
            messages.Single().Text.Should().Be("All new tasks will be sent to you");
        }

        [Fact]
        public async Task Should_Onboard_New_User()
        {
            await _ctx.Chat.SendUserMessage(_botId, _studentId, "/start");
            var message = _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();
            message.Text.Should().Be("Hi");

            await _ctx.Chat.ClickButtonAndWaitUntilHandled(message, "Hi");

            message = _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();
            message.Text.Should().Be("*First Task*\r\nDescription");
        }

        [Fact]
        public async Task Should_Handle_Concurrency()
        {
            await _ctx.Chat.SendUserMessage(_botId, _studentId, "/start");
            var message = _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();

            _ctx.GlobalContextState.OnBeforeNextSaveChanges(() => _ctx.Chat.ClickButtonAndWaitUntilHandled(message, "Hi"));
            await _ctx.Chat.ClickButtonAndWaitUntilHandled(message, "Hi");

            var messages = _ctx.Chat.ReadNewBotMessages(_botId, _studentId);
            messages.Should().HaveCount(2);
            // Message should be duplicated because we don't support idenpotency for message sending in Workflow
            // To make message single we must store message to database before sending and have idenpotency key of message
            foreach (var msg in messages)
                msg.Text.Should().Be("*First Task*\r\nDescription"); 
        }

        [Fact]
        public async Task Should_Change_Task_Status()
        {
            var taskMessage = await StartFirstTask();
            await _ctx.Chat.ClickButton(taskMessage, taskMessage.Buttons!.First().Key);

            var requestCommentMessage = _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();
            requestCommentMessage.Text.Should().Be("Please send message to this chat");
            await _ctx.Chat.SendUserMessage(_botId, _studentId, "Result");

            var updatedMessage = _ctx.Chat.ReadMessage(_botId, _studentId, taskMessage.MessageId);
            updatedMessage!.Buttons!.First().Value.Should().Be("☑️");
            taskMessage.Text.Should().Be(updatedMessage.Text);
            taskMessage.Buttons.Should().NotBeEquivalentTo(updatedMessage.Buttons);
        }

        [Fact]
        public async Task Should_Add_Comment_On_Changing_Task_Status()
        {
            var taskMessage = await StartFirstTask();
            await _ctx.Chat.ClickButton(taskMessage, taskMessage.Buttons!.First().Key);

            var requestComment = _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();
            requestComment.Text.Should().Be("Please send message to this chat");

            await _ctx.Chat.SendUserMessage(
                _botId,
                _studentId,
                "Here is my result:  https://www.figma.com/file/65UWsCMvohKGerVrUWWFdd/Task-Workflow?type=whiteboard&node-id=0-1&t=HtMQYV7kgZbTx2GO-0"
            );
            var commentMessage = _ctx.Chat.ReadNewBotMessageTexts(_botId, _studentId).Single();
            commentMessage.Should().Contain("Boris");
            commentMessage.Should().Contain("Here is my result:");

            var updatedMessage = _ctx.Chat.ReadMessage(_botId, _studentId, taskMessage.MessageId);
            updatedMessage!.Buttons!.First().Value.Should().Be("☑️");
            taskMessage.Text.Should().Be(updatedMessage.Text);
            taskMessage.Buttons.Should().NotBeEquivalentTo(updatedMessage.Buttons);
        }

        [Fact]
        public async Task Should_Update_Task_Status_Twice()
        {
            var taskMessage = await StartFirstTask();

            await _ctx.Chat.ClickButton(taskMessage, taskMessage.Buttons!.Last().Key);
            await _ctx.Chat.SendUserMessage(_botId, _studentId, "Some comment");
            _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();

            await _ctx.Chat.ClickButton(taskMessage, taskMessage.Buttons!.First().Key);
            var confirmationMessage = _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single(); // Not add comment
            await _ctx.Chat.ClickButton(confirmationMessage, confirmationMessage.Buttons!.Last().Key);

            var updatedMessage = _ctx.Chat.ReadMessage(_botId, _studentId, taskMessage.MessageId);
            updatedMessage!.Buttons!.First().Value.Should().Be("☑️");

            await _ctx.Chat.ClickButton(updatedMessage, updatedMessage.Buttons!.First().Key);
            confirmationMessage = _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single(); // Not add comment
            await _ctx.Chat.ClickButton(confirmationMessage, confirmationMessage.Buttons!.Last().Key);

            var updatedMessage2 = _ctx.Chat.ReadMessage(_botId, _studentId, taskMessage.MessageId);
            updatedMessage2!.Buttons!.First().Value.Should().NotBe(updatedMessage.Buttons!.First().Value);
        }

        [Fact]
        public async Task Should_Hide_Task_Buttons_When_Confirmation_Is_Shown()
        {
            var taskMessage = await StartFirstTask();

            await _ctx.Chat.ClickButton(taskMessage, taskMessage.Buttons!.First().Key);

            var updatedMessage = _ctx.Chat.ReadMessage(_botId, _studentId, taskMessage.MessageId);
            updatedMessage!.Buttons.Should().BeNull();
        }

        [Fact]
        public async Task Should_Add_Comment_To_Task()
        {
            var taskMessage = await StartFirstTask();
            await _ctx.Chat.ClickButton(taskMessage, taskMessage.Buttons!.Last().Key);

            var message = _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();
            message.Text.Should().Be("Please send message to this chat");

            await _ctx.Chat.SendUserMessage(_botId, _studentId, "Here is my result: https://wisk.pro");

            _ctx.Chat.ReadMessage(_botId, _studentId, message.MessageId).Should().BeNull();

            var commentMessage = _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();
            commentMessage.ReplyToMessageId.Should().Be(taskMessage.MessageId);
            commentMessage.Text.Should().Be($"(Boris Sotsky)[tg://user/{_studentId}]:\r\nHere is my result: https://wisk.pro");
        }

        [Fact]
        public async Task Should_Hide_Comment_Dialog_On_Timeout()
        {
            var taskMessage = await StartFirstTask();
            await _ctx.Chat.ClickButton(taskMessage, taskMessage.Buttons!.Last().Key);

            var message = _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();
            message.Text.Should().Be("Please send message to this chat");

            await _ctx.Scheduler.RewindTime(TimeSpan.FromMinutes(10));
            _ctx.Chat.ReadMessage(_botId, _studentId, message.MessageId).Should().BeNull();
        }

        [Fact]
        public async Task Should_Hide_Comment_Dialog_On_Cancel()
        {
            var taskMessage = await StartFirstTask();
            await _ctx.Chat.ClickButton(taskMessage, taskMessage.Buttons!.Last().Key);

            var message = _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();
            message.Text.Should().Be("Please send message to this chat");

            await _ctx.Chat.SendUserMessage(_botId, _studentId, "/cancel");
            _ctx.Chat.ReadMessage(_botId, _studentId, message.MessageId).Should().BeNull();
        }

        [Fact]
        public async Task Should_Show_Confirmed_Task_To_Curator()
        {
            await AddCurator();

            await SubmitFirtTask("Here is my result: https://wisk.pro");

            await _ctx.AsyncWait.For(() =>
            {
                var curatorMessages = _ctx.Chat.ReadNewBotMessages(_botId, _curatorId);
                curatorMessages.Should().HaveCount(2);

                curatorMessages.First().Text.Should().Be("*First Task*\r\nDescription");
                curatorMessages.Last().Text.Should().Contain("Boris Sotsky");
                curatorMessages.Last().Text.Should().Contain("Here is my result: https://wisk.pro");
            });
        }

        [Fact]
        public async Task Should_Not_Show_Confirmed_Task_To_Curator_If_Comment_Was_Not_Added()
        {
            await AddCurator();

            await SubmitFirtTask(null);

            var curatorMessages = _ctx.Chat.ReadNewBotMessages(_botId, _curatorId);
            curatorMessages.Should().HaveCount(0);
        }

        [Fact]
        public async Task Should_Show_Confirmed_Task_To_Curator_If_Comment_Was_Added_Later()
        {
            await AddCurator();

            var studentTaskMessage = await SubmitFirtTask(null);
            var requestComment = _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();
            requestComment.Text.Should().Be("Please send message to this chat");

            var curatorMessages = _ctx.Chat.ReadNewBotMessages(_botId, _curatorId);
            curatorMessages.Should().HaveCount(0);

            await _ctx.Chat.SendUserMessage(_botId, _studentId, "Result");

            await _ctx.AsyncWait.For(() =>
            {
                curatorMessages = _ctx.Chat.ReadNewBotMessages(_botId, _curatorId);
                curatorMessages.Should().HaveCount(2);
            });
        }

        [Fact]
        public async Task Should_Show_Comments_From_Curator()
        {
            await AddCurator();

            var studentTaskMessage = await SubmitFirtTask("Result");

            await _ctx.AsyncWait.For(async () =>
            {
                var messages = _ctx.Chat.ReadNewBotMessages(_botId, _curatorId);

                var taskMessage = messages.First();
                await _ctx.Chat.ClickButton(taskMessage, taskMessage.Buttons!.Last().Key);
            });

            await _ctx.Chat.SendUserMessage(_botId, _curatorId, "Well done!");

            await _ctx.AsyncWait.For(() =>
            {
                var reply = _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();
            });
        }

        private async Task<IReadOnlyCollection<BotFrameworkMessage>> AddCurator()
        {
            await _ctx.Chat.SendUserMessage(_botId, _curatorId, "/curator");
            return _ctx.Chat.ReadNewBotMessages(_botId, _curatorId);
        }

        private async Task<BotFrameworkMessage> StartFirstTask()
        {
            await _ctx.Chat.SendUserMessage(_botId, _studentId, "/start");
            var message = _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();
            message.Text.Should().Be("Hi");

            await _ctx.Chat.ClickButtonAndWaitUntilHandled(message, "Hi");

            return _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();
        }

        private async Task<BotFrameworkMessage> SubmitFirtTask(string? resultText)
        {
            var taskMessage = await StartFirstTask();

            if (resultText != null)
            {
                await _ctx.Chat.ClickButton(taskMessage, taskMessage.Buttons!.Last().Key); // Comment
                _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();

                await _ctx.Chat.SendUserMessage(_botId, _studentId, resultText);
                _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single(); // Comment
            }

            await _ctx.Chat.ClickButton(taskMessage, taskMessage.Buttons!.First().Key); // Submit
            if (resultText != null)
            {
                var confirmation = _ctx.Chat.ReadNewBotMessages(_botId, _studentId).Single();
                await _ctx.Chat.ClickButton(confirmation, confirmation.Buttons!.Last().Value);
            }

            return taskMessage;
        }

        private async Task HandleUserMessage(BotFrameworkMessage message)
        {
            var userContext = await _ctx.UserContextRepository.GetUserOrCreateContext(message.BotId, message.ChatId,
                message.UserInfo.Name,
                message.UserInfo.Username ?? message.UserInfo.UserId.ToString()
            );

            if (string.Equals(message.Text, "/start", StringComparison.OrdinalIgnoreCase))
                await _ctx.WorkflowManager.Start(userContext).Workflow<OnboardingWorkflow>();
            else if (string.Equals(message.Text, "/curator", StringComparison.OrdinalIgnoreCase))
                await _ctx.WorkflowManager.Start(userContext).Workflow<CuratorWorkflow>();
            else if (string.Equals(message.Text, "/cancel", StringComparison.OrdinalIgnoreCase))
                await _ctx.WorkflowManager.RemoveDefaultSesssions(null, userContext.ContextId.ToString());
            else
                await _ctx.WorkflowManager.HandleEvent(message);
        }

        private async Task HandleButtonClick(BotFrameworkButtonClick buttonClick)
        {
            await _ctx.WorkflowManager.HandleEvent(buttonClick);
        }

        private async Task HandleEvent<TEvent>(TEvent ev)
            where TEvent: class
        {
            await _ctx.WorkflowManager.HandleEvent(ev);
        }

        class OnboardingWorkflow : Workflow
        {
            public const string Id = nameof(OnboardingWorkflow);

            private readonly ChatFake _chat;
            private readonly TaskRepository _taskRepository;
            private readonly WorkflowManagerAccessor<UserContext> _workflowManagerAccessor;

            public override string WorkflowId => Id;

            public OnboardingWorkflow(ChatFake chat, TaskRepository taskRepository, WorkflowManagerAccessor<UserContext> workflowManagerAccessor)
            {
                _chat = chat;
                _taskRepository = taskRepository;
                _workflowManagerAccessor = workflowManagerAccessor;
            }

            public override IFlow<Unit> Execute(IFlow<Unit> flow)
            {
                var context = flow.Scope.GetContext<UserContext>();
                return flow.SelectAsync(async () =>
                {
                    return await _chat.SendButtonsBotMessage(context.BotId, context.ChatId, "Hi", new KeyValuePair<string, string>("Hi", "Hi"));
                }).Persist("WelcomeMessage")
                .StopAndWait().For<BotFrameworkButtonClick>("Hi", messageId => new BotFrameworkButtonClickAwaiter(context, messageId))
                .SelectAsync(async (_, scope) =>
                {
                    var context = scope.GetContext<UserContext>();
                    var taskId = await _taskRepository.CreateTask("First Task", "Description", context.UserId);
                    await _workflowManagerAccessor.WorkflowManager.Start(context, taskId).Workflow<TaskWorkflow>();
                })
                .MapToVoid();
            }
        }

        class CuratorWorkflow : Workflow
        {
            public const string Id = nameof(CuratorWorkflow);
            private readonly WorkflowManagerAccessor<UserContext> _workflowManagerAccessor;
            private readonly ChatFake _chat;

            public override string WorkflowId => Id;

            public CuratorWorkflow(WorkflowManagerAccessor<UserContext> workflowManagerAccessor, ChatFake chat)
            {
                _workflowManagerAccessor = workflowManagerAccessor;
                _chat = chat;
            }

            public override IFlow<Unit> Execute(IFlow<Unit> input)
            {
                var context = input.Scope.GetContext<UserContext>();
                return input.SelectAsync(() => _chat.SendBotMessage(context.BotId, context.ChatId, "All new tasks will be sent to you"))
                    .Persist("WelcomeMessage")
                    .SelectAsync(async (_, scope) => HandleNewTask(await scope.BeginRecursiveScope("TaskCreatedLoop")));
            }

            private IFlow<Unit> HandleNewTask(StateMachineScope scope)
            {
                return scope.StopAndWait<TaskCreatedEvent>("TaskCreated", TaskCreatedEventAwaiter.Default)
                    .SelectAsync(tc => _workflowManagerAccessor.WorkflowManager.Start(scope.GetContext<UserContext>(), tc.TaskId).Workflow<CuratorTaskWorkflow>())
                    .IncreaseRecoursionDepth()
                    .Select(_ => HandleNewTask(scope));
            }
        }

        class CuratorTaskWorkflow : Workflow<int>
        {
            public const string Id = nameof(CuratorTaskWorkflow);
            private readonly WorkflowManagerAccessor<UserContext> _workflowManagerAccessor;
            private readonly TaskRepository _taskRepository;

            public override string WorkflowId => Id;

            public CuratorTaskWorkflow(WorkflowManagerAccessor<UserContext> workflowManagerAccessor, TaskRepository taskRepository)
            {
                _workflowManagerAccessor = workflowManagerAccessor;
                _taskRepository = taskRepository;
            }

            public override IFlow<Unit> Execute(IFlow<int> input)
            {
                return input.Persist("TaskCreated")
                    .SelectAsync(async (taskId, scope) => WhenTaskReady(taskId, await scope.BeginRecursiveScope("TaskReady")))
                    .SelectAsync((tc, scope) => _workflowManagerAccessor.WorkflowManager.Start(scope.GetContext<UserContext>(), tc.TaskId).Workflow<TaskWorkflow>())
                    .MapToVoid();
            }

            private IFlow<TaskModel> WhenTaskReady(int taskId, StateMachineScope scope)
            {
                return scope.StartFlow(taskId).WhenAny(
                    "TaskReady",
                    inner => inner.StopAndWait().For<TaskStateChanged>("StateChange", taskId => new TaskStateChangedAwaiter(taskId), (tsc) =>
                    {
                        return tsc.TaskId == taskId && tsc.State == TaskState.ReadyForReview;
                    }).MapTo(taskId),
                    inner => inner.StopAndWait().For<TaskCommentAdded>("CommentAdded", taskId => new TaskCommentAddedAwaiter(taskId), (ta) =>
                    {
                        return ta.TaskId == taskId;
                    }).MapTo(taskId)
                ).SelectAsync(_taskRepository.GetTask)
                .Select(task =>
                {
                    if (task.State == TaskState.ReadyForReview && task.Comments.Count > 0)
                        return scope.StartFlow(task);

                    return scope.StartFlow(task.TaskId)
                        .Persist("TaskNotReady")
                        .IncreaseRecoursionDepth()
                        .Select(taskId => WhenTaskReady(taskId, scope));
                });
            }
        }

        partial class TaskWorkflow : Workflow<int>
        {
            private readonly TaskRepository _taskRepository;
            private readonly ChatFake _chat;
            private readonly RequestCommentControl _requestComment;
            private readonly ShowCommentControl _showCommentControl;
            private readonly ConfirmationControl _confirmationControl;

            public TaskWorkflow(TaskRepository taskRepository, ChatFake chatFake,
                RequestCommentControl requestComment, ShowCommentControl showCommentControl, ConfirmationControl confirmationControl)
            {
                _taskRepository = taskRepository;
                _chat = chatFake;
                _requestComment = requestComment;
                _showCommentControl = showCommentControl;
                _confirmationControl = confirmationControl;
            }

            public const string Id = nameof(TaskWorkflow);
            public override string WorkflowId => Id;

            public override IFlow<Unit> Execute(IFlow<int> input)
            {
                return input.Select((taskId, scope) => ShowTask(taskId, scope))
                    .Persist("TaskShown")
                    .SelectAsync(async (tmc, scope) => HandleTask(tmc, await scope.BeginRecursiveScope("TaskEvents")));
            }

            private IFlow<Unit> HandleTask(TaskMessageContext taskMessageContext, StateMachineScope scope)
            {
                return scope.WhenAny(
                    "TaskEvents",
                    innerScope =>
                    {
                        var userContext = scope.GetContext<UserContext>();
                        return innerScope.StopAndWait<BotFrameworkButtonClick>("Click", new BotFrameworkButtonClickAwaiter(userContext, taskMessageContext.MessageId))
                            .Select(click =>
                            {
                                var query = WorkflowCallbackQuery.Parse(click.SelectedValue);
                                switch (query.Command)
                                {
                                    case "cs":
                                        {
                                            var state = (TaskState)int.Parse(query.Parameters!["s"]);
                                            return scope.StartFlow(() => _taskRepository.UpdateTaskState(
                                                taskMessageContext.TaskId,
                                                state,
                                                new Dictionary<string, string>
                                                {
                                                    ["SessionId"] = scope.SessionId.ToString("n")
                                                })
                                           ).Persist("StateUpdated")
                                           .Select(task =>
                                           {
                                               return UpdateTask(taskMessageContext.MessageId, task, scope, false)
                                                .MapTo(task);
                                           })
                                           .Persist("RemovedTaskButtons")
                                           .Select(task =>
                                           {
                                               return (task.Comments.Count != 0 ? _confirmationControl.StartDialog(
                                                                                                  scope.BeginScope("ConfirmComment"),
                                                                                                  new DialogConfiguration("Do you want to add comment with result?")
                                                                                               )
                                               : scope.StartFlow(true))
                                                .Select(addComment =>
                                                {
                                                    if (addComment)
                                                        return _requestComment.StartDialog(scope.BeginScope("FinalComment"), taskMessageContext, task.Comments.Count == 0);

                                                    return scope.StartFlow();
                                                })
                                                .Select(_ => UpdateTask(taskMessageContext.MessageId, task, scope, true));
                                           });
                                        }
                                    case "c":
                                        {
                                            return _requestComment.StartDialog(scope.BeginScope("Comment"), taskMessageContext, false);
                                        }
                                    default:
                                        throw new NotSupportedException($"Not supported command {query.Command} workflow {scope.SessionId}");
                                };
                            });
                    },
                    innerScope =>
                    {
                        return innerScope.StopAndWait<TaskCommentAdded>("CommentAdded", new TaskCommentAddedAwaiter(taskMessageContext.TaskId))
                            .SelectAsync(comment => _showCommentControl.ShowComment(innerScope.GetContext<UserContext>(), new CommentModel
                            {
                                CommentId = comment.CommentId,
                                Text = comment.Text,
                                UserId = comment.UserId
                            }, taskMessageContext.MessageId)
                        ).MapToVoid();
                    }
                )
                .IncreaseRecoursionDepth()
                .Select(_ => HandleTask(taskMessageContext, scope))
                .MapToVoid();
            }

            private IFlow<Unit> UpdateTask(int messageId, TaskModel? task, StateMachineScope scope, bool showButtons)
            {
                return scope.StartFlow(async () =>
                {
                    var userContext = scope.GetContext<UserContext>();
                    if (task != null)
                        await _chat.UpdateBotMessage(userContext.BotId, userContext.ChatId, messageId, GetTaskMessage(scope, task),
                           showButtons ? GetButtons(scope, task).ToArray() : new KeyValuePair<string, string>[0]
                        );
                });
            }

            private IFlow<TaskMessageContext> ShowTask(int taskId, StateMachineScope scope)
            {
                return scope.StartFlow(taskId)
                    .SelectAsync(_taskRepository.GetTask)
                    .SelectAsync(async task =>
                    {
                        var context = scope.GetContext<UserContext>();
                        var messageId = await _chat.SendButtonsBotMessage(
                            context.BotId,
                            context.ChatId,
                            GetTaskMessage(scope, task),
                            GetButtons(scope, task).ToArray()
                        );

                        foreach (var c in task.Comments)
                            await _showCommentControl.ShowComment(context, c, messageId);

                        return new TaskMessageContext { TaskId = taskId, MessageId = messageId };
                    });
            }

            private string GetTaskMessage(StateMachineScope scope, TaskModel task)
            {
                return $"*{task.Title}*\r\n{task.Description}";
            }

            private IEnumerable<KeyValuePair<string, string>> GetButtons(StateMachineScope scope, TaskModel task)
            {
                yield return new KeyValuePair<string, string>(GetCompleteButtonQuery(scope, task).ToString(), GetCompleteButtonText(scope, task));
                yield return new KeyValuePair<string, string>(
                    new WorkflowCallbackQuery { Command = "c", SessionId = scope.SessionState.SessionStateId }.ToString(),
                    "💬 Comment"
                );
            }

            private WorkflowCallbackQuery GetCompleteButtonQuery(StateMachineScope scope, TaskModel task)
            {
                var userContext = scope.GetContext<UserContext>();
                TaskState nextState = task.State switch
                {
                    TaskState.ToDo or TaskState.InProgress => userContext.UserId == task.AssigneeId ? TaskState.ReadyForReview : TaskState.Approved,
                    TaskState.ReadyForReview => userContext.UserId == task.AssigneeId ? TaskState.InProgress : TaskState.Approved,
                    TaskState.Approved => userContext.UserId == task.AssigneeId ? TaskState.InProgress : TaskState.ToDo,
                    _ => throw new NotSupportedException($"Not supported {nameof(TaskState)}.{task.State}")
                };

                return new WorkflowCallbackQuery
                {
                    SessionId = scope.SessionState.SessionStateId,
                    Command = "cs",
                    Parameters = new Dictionary<string, string> { ["s"] = ((int)nextState).ToString() }
                };
            }

            private string GetCompleteButtonText(StateMachineScope scope, TaskModel task)
            {
                return task.State switch
                {
                    TaskState.ToDo or TaskState.InProgress => "🔲",
                    TaskState.ReadyForReview => "☑️",
                    TaskState.Approved => "✅",
                    _ => throw new NotSupportedException($"Not supported {nameof(TaskState)}.{task.State}")
                };
            }
        }
    }
}
