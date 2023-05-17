using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Tests.Events;

namespace Rx.Net.StateMachine.Tests.Awaiters
{
    public class TaskCommentAddedAwaiter : IEventAwaiter<TaskCommentAdded>
    {
        private readonly int _taskId;

        public TaskCommentAddedAwaiter(int taskId)
        {
            _taskId = taskId;
        }
        public TaskCommentAddedAwaiter(TaskCommentAdded taskCommentAdded): this(taskCommentAdded.TaskId)
        {
        }
        public string AwaiterId => $"{nameof(TaskCommentAdded)}-{_taskId}";
    }
}
