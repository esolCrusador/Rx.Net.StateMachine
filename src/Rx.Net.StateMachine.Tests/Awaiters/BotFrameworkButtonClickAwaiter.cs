using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;

namespace Rx.Net.StateMachine.Tests.Awaiters
{
    public class BotFrameworkButtonClickAwaiter : IEventAwaiter<BotFrameworkButtonClick>
    {
        public string AwaiterId => $"{nameof(BotFrameworkButtonClick)}-b{BotId}c{ChatId}m{MessageId}";

        public long BotId { get; }
        public long ChatId { get; }
        public int MessageId { get; }

        public BotFrameworkButtonClickAwaiter(BotFrameworkButtonClick botFrameworkButtonClick)
            : this(botFrameworkButtonClick.BotId, botFrameworkButtonClick.ChatId, botFrameworkButtonClick.MessageId)
        {
        }

        public BotFrameworkButtonClickAwaiter(UserContext userContext, int messageId)
            :this(userContext.BotId, userContext.ChatId, messageId)
        {
        }

        private BotFrameworkButtonClickAwaiter(long botId, long chatId, int messageId)
        {
            BotId = botId;
            ChatId = chatId;
            MessageId = messageId;
        }
    }
}
