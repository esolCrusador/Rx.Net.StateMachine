using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Tests.Events;
using System;

namespace Rx.Net.StateMachine.Tests.Awaiters
{
    public class TaskCreatedEventAwaiter : IEventAwaiter<TaskCreatedEvent>
    {
        public string AwaiterId => nameof(TaskCreatedEvent);
        public static readonly TaskCreatedEventAwaiter Default = new TaskCreatedEventAwaiter();
    }
}
