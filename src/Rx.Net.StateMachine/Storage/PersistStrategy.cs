using System;
using System.Collections.Generic;
using System.Text;

namespace Rx.Net.StateMachine.Storage
{
    [Flags]
    public enum PersistStrategy
    {
        Default = PersistFinally,
        PersistFinally = 0,
        PersistEachState = 1,
        PersistEachAwaiter = 2,
        PersistEachEvent = 4,
        PersistEachItem = 8,
        PersistEachTime = PersistFinally | PersistEachState | PersistEachAwaiter | PersistEachEvent | PersistStrategy.PersistEachItem
    }
}
