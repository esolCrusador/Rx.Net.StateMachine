using System;
using System.Collections.Generic;
using System.Text;

namespace Rx.Net.StateMachine
{
    public enum HandlingResult
    {
        Handled,
        Finished,
        Ignored,
        Failed
    }
}
