using System;
using System.Collections.Generic;
using System.Text;

namespace Rx.Net.StateMachine.States
{
    public class SessionStateStep
    {
        public int SequenceNumber { get; }
        public string State { get; }
        public SessionStateStep(string state, int sequenceNumber)
        {
            State = state;
            SequenceNumber = sequenceNumber;
        }
    }
}
