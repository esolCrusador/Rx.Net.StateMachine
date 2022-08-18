using System;

namespace Rx.Net.StateMachine.Tests.Entities
{
    public class SessionEventEntity
    {
        public string Event { get; set; }
        public string EventType { get; set; }
        public bool Handled { get; set; }
        public int SequenceNumber { get; set; }
        public string[] Awaiters { get; set; }
    }
}
