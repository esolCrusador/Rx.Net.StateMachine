using Rx.Net.StateMachine.States;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.EntityFramework.Tests.Tables
{
    public class SessionStateTable
    {
        public Guid SessionStateId { get; set; }

        public string WorkflowId { get; set; }
        public int Counter { get; set; }
        public List<SessionStepTable> Steps { get; set; }
        public List<SessionItemTable> Items { get; set; }
        public List<SessionEventTable> PastEvents { get; set; }
        public List<SessionEventAwaiterTable> Awaiters { get; set; }
        public SessionStateStatus Status { get; set; }
        public string Result { get; set; }
    }
}
