using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Tests.Events;
using System;

namespace Rx.Net.StateMachine.Tests.Awaiters
{
    public class TimeoutEventAwaiter : IEventAwaiter<TimeoutEvent>
    {
        public Guid EventId { get; }
        public TimeoutEventAwaiter(Guid eventId)
        {
            EventId = eventId;
        }
        public TimeoutEventAwaiter(TimeoutEvent timeoutEvent): this(timeoutEvent.EventId)
        {
        }
        public string AwaiterId => $"{nameof(TimeoutEvent)}-{EventId}";
    }
}
