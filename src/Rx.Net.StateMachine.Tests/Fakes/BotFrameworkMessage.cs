using System;
using System.Collections.Generic;

namespace Rx.Net.StateMachine.Tests.Fakes
{
    public class BotFrameworkMessage
    {
        public int MessageId { get; }
        public Guid UserId { get; }
        public MessageSource Source { get; }
        public string Text { get; }
        public IReadOnlyCollection<KeyValuePair<string, string>> Buttons { get; set; }
        public int? ReplyToMessageId { get; set; }
        public BotFrameworkMessage(int messageId, Guid userId, MessageSource source, string text)
        {
            MessageId = messageId;
            UserId = userId;
            Source = source;
            Text = text;
        }
    }
}
