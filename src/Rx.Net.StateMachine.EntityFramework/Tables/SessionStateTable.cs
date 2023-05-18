using Rx.Net.StateMachine.EntityFramework.Tests.Tables;
using Rx.Net.StateMachine.States;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Rx.Net.StateMachine.EntityFramework.Tables
{
    public class SessionStateTable<TContext, TContextKey>
    {
        [Key] public Guid SessionStateId { get; set; }
        public string WorkflowId { get; set; }
        public bool IsDefault { get; set; }
        public int Counter { get; set; }
        public string Steps { get; set; }
        public string Items { get; set; }
        public string PastEvents { get; set; }
        public List<SessionEventAwaiterTable<TContext, TContextKey>> Awaiters { get; set; }
        public SessionStateStatus Status { get; set; }
        public string? Result { get; set; }
        public TContextKey ContextId { get; set; }
        public TContext Context { get; set; }
    }
}
