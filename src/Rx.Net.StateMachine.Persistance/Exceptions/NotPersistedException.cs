using System;

namespace Rx.Net.StateMachine.Persistance.Exceptions
{
    public class NotPersistedException(string message) : Exception(message)
    {
    }
}
