using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Rx.Net.StateMachine.ObservableExtensions;
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
using Rx.Net.StateMachine.Tests.Extensions;
using System.Diagnostics;

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
            public Slow() : base(StateMachineTestContextBuilder.Fast())
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
        public async Task Should_Change_Task_Status()
        {
            var taskMessage = await StartFirstTask();
            await _ctx.Chat.ClickButton(taskMessage, taskMessage.Buttons!.First().Key);

            var updatedMessage = _ctx.Chat.ReadMessage(_botId, _studentId, taskMessage.MessageId);
            updatedMessage!.Buttons!.First().Value.Should().Be("☑️");
            taskMessage.Text.Should().Be(updatedMessage.Text);
            taskMessage.Buttons.Should().NotBeEquivalentTo(updatedMessage.Buttons);
        }

        [Fact]
        public async Task Should_Update_Task_Status_Twice()
        {
            var taskMessage = await StartFirstTask();
            await _ctx.Chat.ClickButton(taskMessage, taskMessage.Buttons!.First().Key);

            var updatedMessage = _ctx.Chat.ReadMessage(_botId, _studentId, taskMessage.MessageId);
            updatedMessage!.Buttons!.First().Value.Should().Be("☑️");

            await _ctx.Chat.ClickButton(updatedMessage, updatedMessage.Buttons!.First().Key);
            var updatedMessage2 = _ctx.Chat.ReadMessage(_botId, _studentId, taskMessage.MessageId);
            updatedMessage2!.Buttons!.First().Value.Should().NotBe(updatedMessage.Buttons!.First().Value);
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

        private async Task HandleUserMessage(BotFrameworkMessage message)
        {
            var userContext = await _ctx.UserContextRepository.GetUserOrCreateContext(message.BotId, message.ChatId,
                message.UserInfo.Name,
                message.UserInfo.Username ?? message.UserInfo.UserId.ToString()
            );

            if (string.Equals(message.Text, "/start", StringComparison.OrdinalIgnoreCase))
                await _ctx.WorkflowManager.StartHandle(OnboardingWorkflow.Id, userContext);
            else if (string.Equals(message.Text, "/curator", StringComparison.OrdinalIgnoreCase))
                await _ctx.WorkflowManager.StartHandle(CuratorWorkflow.Id, userContext);
            else
                await _ctx.WorkflowManager.HandleEvent(message);
        }

        private async Task HandleButtonClick(BotFrameworkButtonClick buttonClick)
        {
            await _ctx.WorkflowManager.HandleEvent(buttonClick);
        }

        private async Task HandleEvent<TEvent>(TEvent ev)
        {
            await _ctx.WorkflowManager.HandleEvent(ev);
        }

        class OnboardingWorkflow : Workflow<Unit, Unit>
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

            public override IObservable<Unit> GetResult(IObservable<Unit> input, StateMachineScope scope)
            {
                return Observable.FromAsync(async () =>
                {
                    var context = scope.GetContext<UserContext>();
                    return await _chat.SendButtonsBotMessage(context.BotId, context.ChatId, "Hi", new KeyValuePair<string, string>("Hi", "Hi"));
                }).Persist(scope, "WelcomeMessage")
                .StopAndWait().For<BotFrameworkButtonClick>(scope, "Hi", (click, messageId) => click.MessageId == messageId)
                .SelectAsync(async () =>
                {
                    var context = scope.GetContext<UserContext>();
                    var taskId = await _taskRepository.CreateTask("First Task", "Description", context.UserId);
                    await _workflowManagerAccessor.WorkflowManager.StartHandle(taskId, TaskWorkflow.Id, context);
                })
                .Concat()
                .MapToVoid();
            }
        }

        class CuratorWorkflow : Workflow<Unit, Unit>
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

            public override IObservable<Unit> GetResult(IObservable<Unit> _, StateMachineScope scope)
            {
                var context = scope.GetContext<UserContext>();
                return Observable.FromAsync(() => _chat.SendBotMessage(context.BotId, context.ChatId, "All new tasks will be sent to you"))
                    .Persist(scope, "WelcomeMessage")
                    .Select(_ => HandleNewTask(scope))
                    .Concat();
            }

            private IObservable<Unit> HandleNewTask(StateMachineScope scope)
            {
                return scope.StopAndWait<TaskCreatedEvent>("TaskCreated")
                    .SelectAsync(tc => _workflowManagerAccessor.WorkflowManager.StartHandle(tc.TaskId, CuratorTaskWorkflow.Id, scope.GetContext<UserContext>()))
                    .Concat()
                    .IncreaseRecoursionDepth(scope)
                    .Select(_ => HandleNewTask(scope))
                    .Concat();
            }
        }

        class CuratorTaskWorkflow : Workflow<int, Unit>
        {
            public const string Id = nameof(CuratorTaskWorkflow);
            private readonly WorkflowManagerAccessor<UserContext> _workflowManagerAccessor;

            public override string WorkflowId => Id;

            public CuratorTaskWorkflow(WorkflowManagerAccessor<UserContext> workflowManagerAccessor)
            {
                _workflowManagerAccessor = workflowManagerAccessor;
            }

            public override IObservable<Unit> GetResult(IObservable<int> input, StateMachineScope scope)
            {
                return input.Persist(scope, "TaskCreated")
                    .StopAndWait().For<TaskStateChanged>(scope, "StateChange", (tsc, taskId) => tsc.TaskId == taskId && tsc.State == TaskState.ReadyForReview)
                    .SelectAsync(tc => _workflowManagerAccessor.WorkflowManager.StartHandle(tc.TaskId, TaskWorkflow.Id, scope.GetContext<UserContext>()))
                    .MapToVoid();
            }
        }

        class TaskWorkflow : Workflow<int, Unit>
        {
            private readonly TaskRepository _taskRepository;
            private readonly UserContextRepository _userContextRepository;
            private readonly ChatFake _chat;
            private readonly FakeScheduler _scheduler;

            public TaskWorkflow(TaskRepository taskRepository, UserContextRepository userContextRepository, ChatFake chatFake, FakeScheduler scheduler)
            {
                _taskRepository = taskRepository;
                _userContextRepository = userContextRepository;
                _chat = chatFake;
                _scheduler = scheduler;
            }

            public const string Id = nameof(TaskWorkflow);
            public override string WorkflowId => Id;

            public override IObservable<Unit> GetResult(IObservable<int> input, StateMachineScope scope)
            {
                return input.Select(taskId => ShowTask(taskId, scope)).Concat()
                    .Persist(scope, "TaskShown")
                    .SelectAsync(async tmc => HandleTask(tmc, await scope.BeginRecursiveScope("TaskEvents")))
                    .Concat()
                    .Concat();
            }

            private IObservable<Unit> HandleTask(TaskMessageContext taskMessageContext, StateMachineScope scope)
            {
                return scope.WhenAny(
                    "TaskEvents",
                    innerScope =>
                    {
                        return innerScope.StopAndWait<BotFrameworkButtonClick>("Click")
                            .Select(click =>
                            {
                                var query = WorkflowCallbackQuery.Parse(click.SelectedValue);
                                switch (query.Command)
                                {
                                    case "cs":
                                        {
                                            var state = (TaskState)int.Parse(query.Parameters!["s"]);
                                            return Observable.FromAsync(() => _taskRepository.UpdateTaskState(
                                                taskMessageContext.TaskId,
                                                state,
                                                new Dictionary<string, string>
                                                {
                                                    ["SessionId"] = scope.SessionId.ToString("n")
                                                })
                                           );
                                        }
                                    case "c":
                                        {
                                            return AddComment(taskMessageContext, scope.BeginScope($"Comment"));
                                        }
                                    default:
                                        throw new NotSupportedException($"Not supported command {query.Command} workflow {scope.SessionId}");
                                };
                            }).Concat();
                    }
                ).Select(task => UpdateTask(taskMessageContext.MessageId, task, scope))
                .Concat()
                .Persist(scope, "TaskUpdated")
                .IncreaseRecoursionDepth(scope)
                .Select(_ => HandleTask(taskMessageContext, scope))
                .Concat()
                .MapToVoid();
            }

            private IObservable<TaskModel?> AddComment(TaskMessageContext taskMessageContext, StateMachineScope scope)
            {
                var userContext = scope.GetContext<UserContext>();

                return Observable.FromAsync(async () =>
                {
                    await scope.MakeDefault(true);

                    return await _chat.SendBotMessage(userContext.BotId, userContext.ChatId, "Please send message to this chat");
                })
                .Persist(scope, "CommentRequested")
                .PersistMessageId(scope)
                .WhenAny(
                    scope,
                    "UserMessageOrTimeout",
                    (messageId, innerScope) =>
                        innerScope.StopAndWait<BotFrameworkMessage>("UserMessage")
                        .SelectAsync(async message =>
                        {
                            var comment = await _taskRepository.AddComment(
                                taskMessageContext.TaskId,
                                userContext.UserId,
                                message.Text,
                                new Dictionary<string, string> { ["SessionId"] = innerScope.SessionId.ToString("n") }
                            );

                            await _chat.DeleteUserMessage(userContext.BotId, userContext.ChatId, message.MessageId);
                            await ShowComment(userContext, comment, taskMessageContext.MessageId);
                        })
                        .Concat(),
                    (messageId, innerScope) =>
                        Observable.FromAsync(async () =>
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
                        .StopAndWait().For<TimeoutEvent>(innerScope, "Timeout", (timeout, eventId) =>
                        {
                            return timeout.EventId == eventId;
                        })
                        .MapToVoid()
                )
                .FinallyAsync(async (isExecuted, el, ex) =>
                {
                    if (isExecuted)
                        await scope.MakeDefault(false);
                })
                .DeleteMssages(scope, _chat)
                .Take(1)
                .MapTo((TaskModel?)null);
            }

            class TaskMessageContext
            {
                public int TaskId { get; set; }
                public int MessageId { get; set; }
            }

            private IObservable<Unit> UpdateTask(int messageId, TaskModel? task, StateMachineScope scope)
            {
                return Observable.FromAsync(async () =>
                {
                    var userContext = scope.GetContext<UserContext>();
                    if (task != null)
                        await _chat.UpdateBotMessage(userContext.BotId, userContext.ChatId, messageId, GetTaskMessage(scope, task), GetButtons(scope, task).ToArray());
                });
            }

            private IObservable<TaskMessageContext> ShowTask(int taskId, StateMachineScope scope)
            {
                return StateMachineObservableExtensions.Of(taskId)
                    .SelectAsync(_taskRepository.GetTask)
                    .Concat()
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
                            await ShowComment(context, c, messageId);

                        return new TaskMessageContext { TaskId = taskId, MessageId = messageId };
                    })
                    .Concat();
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

            private async Task<int> ShowComment(UserContext userContext, CommentModel comment, int taskMessageId)
            {
                var user = await _userContextRepository.GetUserContext(comment.UserId)
                    ?? throw new Exception("User not found");
                return await _chat.SendBotMessage(
                    userContext.BotId,
                    userContext.ChatId,
                    $"({user.Name})[tg://user/{user.ChatId}]:\r\n{comment.Text}",
                    taskMessageId
                );
            }
        }
    }
}
