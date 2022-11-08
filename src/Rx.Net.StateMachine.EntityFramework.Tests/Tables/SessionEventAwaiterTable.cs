using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.EntityFramework.Tests.Tables
{
    public class SessionEventAwaiterTable
    {
        public Guid AwaiterId { get; set; }
        public Guid SessionStateId { get; set; }
        public string TypeName { get; set; }
        public int SequenceNumber { get; set; }
    }
}
