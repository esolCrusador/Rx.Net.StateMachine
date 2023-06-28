using System;

namespace Rx.Net.StateMachine.Persistance.Exceptions
{
    public class ConcurrencyException: Exception
    {
        public ConcurrencyException(string message, Exception innerException)
            :base(message, innerException)
        {
        }
    }
}
