using System;

namespace Rx.Net.StateMachine.EntityFramework.Tests.Tables
{
    public class SessionEventAwaiterTable<TContext, TContextKey>
    {
        public Guid AwaiterId { get; set; }
        public Guid SessionStateId { get; set; }
        public string TypeName { get; set; }
        public int SequenceNumber { get; set; }
        public TContextKey ContextId { get; set; }
        public TContext Context { get; set; }
    }
}
