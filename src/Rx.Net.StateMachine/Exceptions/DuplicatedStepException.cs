using System;

namespace Rx.Net.StateMachine.Exceptions
{
    public class DuplicatedStepException : Exception
    {
        public DuplicatedStepException(string stateId) : base($"The state {stateId} is duplicated")
        {
        }
    }
}
