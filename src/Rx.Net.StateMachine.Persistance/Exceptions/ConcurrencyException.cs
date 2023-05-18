using System;

namespace Rx.Net.StateMachine.Persistance.Exceptions
{
    public class ConcurrencyException: Exception
    {
        public ConcurrencyException(Exception innerException)
            :base(innerException.Message, innerException)
        {
        }
    }
}
