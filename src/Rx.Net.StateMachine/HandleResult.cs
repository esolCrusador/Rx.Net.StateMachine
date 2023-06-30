using System;

namespace Rx.Net.StateMachine
{
    public class HandlingResult
    {
        public Guid? SessionId { get; }
        public HandlingStatus Status { get; }
        public int PassedSteps { get; }
        public HandlingResult(Guid? sessionId, HandlingStatus status, int passedSteps)
        {
            SessionId = sessionId;
            Status = status;
            PassedSteps = passedSteps;
        }
        public static HandlingResult Ignored(Guid sessionId) =>
            new HandlingResult(sessionId, HandlingStatus.Ignored, 0);
    }

    public enum HandlingStatus
    {
        Handled,
        Finished,
        Ignored,
        Failed
    }
}
