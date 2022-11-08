using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.EntityFramework.Tests.Tables
{
    public class SessionEventTable
    {
        public Guid Id { get; set; }
        public Guid SessionStateId { get; set; }

        public string Event { get; set; }
        public string EventType { get; set; }
        public bool Handled { get; set; }
        public int SequenceNumber { get; set; }
    }
}
