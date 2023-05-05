using System;
using System.ComponentModel.DataAnnotations;

namespace Rx.Net.StateMachine.EntityFramework.Tests.Tables
{
    public class SessionEventAwaiterTable<TContext, TContextKey>
    {
        [Key] public Guid AwaiterId { get; set; }
        public Guid SessionStateId { get; set; }
        [StringLength(128)] public string Name { get; set; }
        [StringLength(256)] public string TypeName { get; set; }
        public int SequenceNumber { get; set; }
        public TContextKey ContextId { get; set; }
        public TContext Context { get; set; }
    }
}
