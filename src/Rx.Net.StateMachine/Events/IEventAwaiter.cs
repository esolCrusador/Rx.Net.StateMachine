namespace Rx.Net.StateMachine.Events
{
    public interface IEventAwaiter
    {
        public string AwaiterId { get; }
    }

    public interface IEventAwaiter<TEvent> : IEventAwaiter
    {
    }
}
