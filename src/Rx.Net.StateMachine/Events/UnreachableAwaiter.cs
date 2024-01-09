namespace Rx.Net.StateMachine.Events
{
    public class UnreachableAwaiter : IEventAwaiter<Unreachable>
    {
        public string AwaiterId => nameof(Unreachable);
    }
}
