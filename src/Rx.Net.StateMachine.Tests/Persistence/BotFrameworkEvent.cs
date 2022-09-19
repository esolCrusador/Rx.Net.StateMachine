using System;
using System.Collections.Generic;
using System.Text;

namespace Rx.Net.StateMachine.Tests.Persistence
{
    public class BotFrameworkButtonClick
    {
        public Guid UserId { get; set; }
        public int MessageId { get; set; }
        public string SelectedValue { get; set; }
    }
}
