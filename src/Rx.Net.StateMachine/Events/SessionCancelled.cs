using System;
using System.ComponentModel;

namespace Rx.Net.StateMachine.Events
{
    public class SessionCancelled
    {
        public Guid SessionId { get; }
        public CancellationReason Reason { get; }
        public SessionCancelled(Guid sessionId, CancellationReason reason)
        {
            SessionId = sessionId;
            Reason = reason;
        }
    }

    public enum CancellationReason
    {
        Expired,
        SingleAllowed,
        ParentFinished,
        NewVersionIssued,
    }
}
