using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Tests.Fakes;

namespace Rx.Net.StateMachine.Tests.Awaiters
{
    public class BotFrameworkMessageAwaiter : IEventAwaiter<BotFrameworkMessage>
    {
        public string AwaiterId => nameof(BotFrameworkMessage);

        public static readonly BotFrameworkMessageAwaiter Default = new BotFrameworkMessageAwaiter();
    }
}
