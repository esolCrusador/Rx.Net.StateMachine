using System;

namespace Rx.Net.StateMachine.Tests
{
    public class BotFrameworkMessage
    {
        public int MessageId { get; }
        public Guid UserId { get;}
        public string Text { get; }
        public BotFrameworkMessage(int messageId, Guid userId, string text)
        {
            MessageId = messageId;
            UserId = userId;
            Text = text;
        }
    }
}
