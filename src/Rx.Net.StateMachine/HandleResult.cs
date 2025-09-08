using System;

namespace Rx.Net.StateMachine
{
    public class HandlingResult
    {
        public Guid? SessionId { get; }
        public HandlingStatus Status { get; }
        public int PassedSteps { get; }
        public string? Result { get; }
        public Exception? Exception { get; }
        public HandlingResult(Guid? sessionId, HandlingStatus status, int passedSteps, string? result, Exception? exception)
        {
            SessionId = sessionId;
            Status = status;
            PassedSteps = passedSteps;
            Result = result;
            Exception = exception;
        }
        public static HandlingResult Ignored(Guid sessionId) =>
            new HandlingResult(sessionId, HandlingStatus.Ignored, 0, null, null);
    }

    public enum HandlingStatus
    {
        Handled,
        Finished,
        Ignored,
        Failed
    }
}
