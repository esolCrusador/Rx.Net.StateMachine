using System;

namespace Rx.Net.StateMachine
{
    public class HandlingResult
    {
        public Guid? SessionId { get; }
        public HandlingStatus Status { get; }
        public object UserContext { get; }
        public int PassedSteps { get; }
        public string? Result { get; }
        public Exception? Exception { get; }
        public HandlingResult(Guid? sessionId, HandlingStatus status, int passedSteps, object userContext, string? result, Exception? exception)
        {
            SessionId = sessionId;
            Status = status;
            PassedSteps = passedSteps;
            UserContext = userContext;
            Result = result;
            Exception = exception;
        }
        public static HandlingResult Ignored(Guid sessionId, object userContext) =>
            new HandlingResult(sessionId, HandlingStatus.Ignored, 0, userContext, null, null);
    }

    public enum HandlingStatus
    {
        Handled,
        Finished,
        Ignored,
        Failed
    }
}
