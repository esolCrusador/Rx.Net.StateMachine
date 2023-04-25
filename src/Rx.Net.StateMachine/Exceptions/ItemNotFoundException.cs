using System;

namespace Rx.Net.StateMachine.Exceptions
{
    public class ItemNotFoundException : Exception
    {
        public ItemNotFoundException(string itemId): base($"Item {itemId} was not found")
        {
        }
    }
}
