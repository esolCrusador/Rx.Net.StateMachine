using System;
using System.Collections.Generic;

namespace Rx.Net.StateMachine.Tests.Fakes
{
    public class BotFrameworkMessage
    {
        public int MessageId { get; }
        public long BotId { get; }
        public long ChatId { get; set; }
        public MessageSource Source { get; }
        public string Text { get; }
        public IReadOnlyCollection<KeyValuePair<string, string>>? Buttons { get; set; }
        public int? ReplyToMessageId { get; set; }
        public UserInfo UserInfo { get; set; }
        public BotFrameworkMessage(int messageId, long botId, long chatId, MessageSource source, string text, UserInfo userInfo)
        {
            MessageId = messageId;
            Source = source;
            Text = text;
            BotId = botId;
            ChatId = chatId;
            UserInfo = userInfo;
        }
    }
}
