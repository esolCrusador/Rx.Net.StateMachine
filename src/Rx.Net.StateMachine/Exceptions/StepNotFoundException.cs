using System;
using System.Collections.Generic;
using System.Text;

namespace Rx.Net.StateMachine.Exceptions
{
    public class StepNotFoundException: Exception
    {
        public StepNotFoundException(string stepId): base($"Step {stepId} was not found")
        {
        }
    }
}
