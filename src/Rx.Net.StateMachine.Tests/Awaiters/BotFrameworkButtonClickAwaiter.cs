using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Tests.Fakes;

namespace Rx.Net.StateMachine.Tests.Awaiters
{
    public class BotFrameworkButtonClickAwaiter : IEventAwaiter<BotFrameworkButtonClick>
    {
        public string AwaiterId => $"{nameof(BotFrameworkButtonClick)}-{MessageId}";

        public int MessageId { get; }

        public BotFrameworkButtonClickAwaiter(BotFrameworkButtonClick botFrameworkButtonClick): this(botFrameworkButtonClick.MessageId)
        {
        }

        public BotFrameworkButtonClickAwaiter(int messageId)
        {
            MessageId = messageId;
        }
    }
}
