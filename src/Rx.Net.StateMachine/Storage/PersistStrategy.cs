using System;

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
        PersistIsDefault = 16,
        PersistEachTime = PersistFinally | PersistEachState | PersistEachAwaiter | PersistEachEvent | PersistEachItem | PersistIsDefault
    }
}
