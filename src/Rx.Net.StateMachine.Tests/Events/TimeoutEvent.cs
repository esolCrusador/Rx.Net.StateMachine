using System;

namespace Rx.Net.StateMachine.Tests.Events
{
    public class TimeoutEvent
    {
        public Guid EventId { get; set; }
        public Guid SessionId { get; set; }
    }
}
