using System;

namespace Rx.Net.StateMachine.Events
{
    public class DefaultSessionRemoved
    {
        public Guid? SessionId { get; set; }
        public string UserContextId { get; set; }
    }
}
