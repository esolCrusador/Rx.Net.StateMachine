using System;

namespace Rx.Net.StateMachine.Events
{
    public class SessionCancelledAwaiter : IEventAwaiter<SessionCancelled>
    {
        public string AwaiterId => $"{nameof(SessionCancelled)}-s{SessionId}";
        public Guid SessionId { get; }

        public SessionCancelledAwaiter(Guid sessionId)
        {
            SessionId = sessionId;
        }
        public SessionCancelledAwaiter(SessionCancelled sessionCancelled) : this(sessionCancelled.SessionId)
        {
        }
    }
}
