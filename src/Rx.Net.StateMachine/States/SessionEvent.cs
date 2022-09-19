using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Rx.Net.StateMachine.States
{
    public class SessionEvent
    {
        public int SequenceNumber { get; }
        public object Event { get; }
        public SessionEventAwaiter[] Awaiters { get; }
        public bool Handled { get; set; }

        public SessionEvent(object eventModel, int sequenceNumber, SessionEventAwaiter[] awaiters)
        {
            Event = eventModel;
            SequenceNumber = sequenceNumber;
            Awaiters = awaiters;
        }
    }
}
