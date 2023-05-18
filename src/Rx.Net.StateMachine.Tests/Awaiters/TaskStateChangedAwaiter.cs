using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Tests.Events;

namespace Rx.Net.StateMachine.Tests.Awaiters
{
    public class TaskStateChangedAwaiter : IEventAwaiter<TaskStateChanged>
    {
        private readonly int _taskId;

        public TaskStateChangedAwaiter(int taskId)
        {
            _taskId = taskId;
        }
        public TaskStateChangedAwaiter(TaskStateChanged taskStateChanged): this(taskStateChanged.TaskId)
        {
        }
        public string AwaiterId => $"{nameof(TaskStateChanged)}-taskId-{_taskId}";
    }
}
