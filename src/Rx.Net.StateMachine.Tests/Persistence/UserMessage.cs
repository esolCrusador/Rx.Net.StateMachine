using System;
using System.Collections.Generic;

namespace Rx.Net.StateMachine.Tests.Persistence
{
    public class BotFrameworkMessage
    {
        public int MessageId { get; }
        public Guid UserId { get; }
        public string Text { get; }
        public IReadOnlyCollection<KeyValuePair<string, string>> Buttons { get; set; }
        public BotFrameworkMessage(int messageId, Guid userId, string text)
        {
            MessageId = messageId;
            UserId = userId;
            Text = text;
        }
    }
}
