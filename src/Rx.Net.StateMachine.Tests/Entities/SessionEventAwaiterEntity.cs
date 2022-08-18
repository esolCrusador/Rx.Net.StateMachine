using System;

namespace Rx.Net.StateMachine.Tests.Entities
{
    public class SessionEventAwaiterEntity
    {
        public Guid AwaiterId { get; set; }
        public string TypeName { get; set; }
        public int SequenceNumber { get; set; }
    }
}
