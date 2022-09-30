using System;
using System.Collections.Generic;
using System.Text;

namespace Rx.Net.StateMachine.States
{
    public enum SessionStateStatus
    {
        Result,
        Created,
        InProgress,
        Failed,
        Completed
    }
}
