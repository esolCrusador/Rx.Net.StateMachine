using System;
using System.Collections.Generic;
using System.Text;

namespace Rx.Net.StateMachine.Exceptions
{
    public class DuplicatedStepException : Exception
    {
        public DuplicatedStepException(string stateId) : base($"The state {stateId} is duplicated")
        {
        }
    }
}
