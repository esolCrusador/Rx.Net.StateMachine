using System;

namespace Rx.Net.StateMachine.Events
{
    public class DefaultSessionRemoved
    {
        public Guid? SessionId { get; set; }
        public required string UserContextId { get; set; }
    }
}
