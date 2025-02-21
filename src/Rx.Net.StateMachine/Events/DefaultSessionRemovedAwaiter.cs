namespace Rx.Net.StateMachine.Events
{
    public class DefaultSessionRemovedAwaiter : IEventAwaiter<DefaultSessionRemoved>
    {
        public string AwaiterId => nameof(DefaultSessionRemoved);
        public static readonly DefaultSessionRemovedAwaiter Default = new DefaultSessionRemovedAwaiter();
    }
}
