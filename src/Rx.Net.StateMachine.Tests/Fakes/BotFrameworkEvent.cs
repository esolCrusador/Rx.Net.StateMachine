using System;

namespace Rx.Net.StateMachine.Tests.Fakes
{
    public class BotFrameworkButtonClick
    {
        public long ChatId { get; set; }
        public long BotId { get; set; }
        public int MessageId { get; set; }
        public required string SelectedValue { get; set; }
    }
}
