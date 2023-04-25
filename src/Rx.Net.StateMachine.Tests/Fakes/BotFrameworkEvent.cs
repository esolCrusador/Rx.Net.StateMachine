using System;

namespace Rx.Net.StateMachine.Tests.Fakes
{
    public class BotFrameworkButtonClick
    {
        public Guid UserId { get; set; }
        public int MessageId { get; set; }
        public string SelectedValue { get; set; }
    }
}
