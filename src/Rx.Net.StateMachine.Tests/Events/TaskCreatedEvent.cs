using System;

namespace Rx.Net.StateMachine.Tests.Events
{
    public class TaskCreatedEvent
    {
        public int TaskId { get; set; }
        public Guid UserId { get; set; }
    }
}
