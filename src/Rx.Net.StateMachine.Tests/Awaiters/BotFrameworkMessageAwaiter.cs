using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;

namespace Rx.Net.StateMachine.Tests.Awaiters
{
    public class BotFrameworkMessageAwaiter : IEventAwaiter<BotFrameworkMessage>
    {
        public long BotId { get; }
        public long ChatId { get; }
        public string AwaiterId => $"{nameof(BotFrameworkMessage)}-b{BotId}c{ChatId}";

        public BotFrameworkMessageAwaiter(BotFrameworkMessage botFrameworkMessage)
            : this(botFrameworkMessage.BotId, botFrameworkMessage.ChatId)
        {
        }
        public BotFrameworkMessageAwaiter(UserContext userContext)
            : this(userContext.BotId, userContext.ChatId)
        {
        }

        private BotFrameworkMessageAwaiter(long botId, long chatId)
        {
        }
    }
}
