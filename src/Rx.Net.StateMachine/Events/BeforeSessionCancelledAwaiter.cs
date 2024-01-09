using System;

namespace Rx.Net.StateMachine.Events
{
    public class BeforeSessionCancelledAwaiter : IEventAwaiter<BeforeSessionCancelled>
    {
        public string AwaiterId => SessionId.ToString("n");

        public Guid SessionId { get; }

        public BeforeSessionCancelledAwaiter(Guid sessionId)
        {
            SessionId = sessionId;
        }
        public BeforeSessionCancelledAwaiter(BeforeSessionCancelled sessionCancelled) : this(sessionCancelled.SessionId)
        {
        }
    }
}
