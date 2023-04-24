using System;

namespace Rx.Net.StateMachine.Exceptions
{
    public class DuplicatedItemException : Exception
    {
        public DuplicatedItemException(string itemId) : base($"The item id {itemId} is duplicated")
        {
        }
    }
}
