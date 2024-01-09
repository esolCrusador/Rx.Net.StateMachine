using System;

namespace Rx.Net.StateMachine.Events
{
    public class BeforeSessionCancelled
    {
        public Guid SessionId { get; }
        public CancellationReason Reason { get; }
        public BeforeSessionCancelled(Guid sessionId, CancellationReason reason)
        {
            SessionId = sessionId;
            Reason = reason;
        }
    }
}
