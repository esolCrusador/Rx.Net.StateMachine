using Rx.Net.StateMachine.EntityFramework.Tests.Tables;
using Rx.Net.StateMachine.States;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        [Timestamp] public byte[] ConcurrencyToken { get; set; } // MsSql concurrency token
        // https://stackoverflow.com/questions/78974688/npgsql-ef-core-concurrency-token-property-gets-included-in-migrations
        // https://www.npgsql.org/efcore/modeling/concurrency.html?tabs=data-annotations
        // Ignore one of them based on database
        [ConcurrencyCheck, Column("xmin", TypeName = "xid")] public uint PostgressConcurrencyToken { get; set; } // PostgreSql row counter
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
