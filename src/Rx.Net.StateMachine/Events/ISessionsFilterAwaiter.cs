namespace Rx.Net.StateMachine.Events
{
    public interface ISessionsFilterAwaiter
    {
        public SessionFilterAwaiterType FilterAwaiterType { get; }
    }

    public enum SessionFilterAwaiterType
    {
        None = 1,
        LatestAwaiter = 2
    }
}
