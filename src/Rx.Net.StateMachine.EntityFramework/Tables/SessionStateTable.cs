using Rx.Net.StateMachine.EntityFramework.Tests.Tables;
using Rx.Net.StateMachine.States;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Rx.Net.StateMachine.EntityFramework.Tables
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public class SessionStateTable<TContext, TContextKey>
    {
        [Key] public Guid SessionStateId { get; set; }
        [StringLength(256)] public string WorkflowId { get; set; }
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
        public DateTimeOffset CrearedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        [Timestamp] public byte[] ConcurrencyToken { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
