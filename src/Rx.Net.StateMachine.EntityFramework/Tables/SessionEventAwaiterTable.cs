using Microsoft.EntityFrameworkCore;
using Rx.Net.StateMachine.EntityFramework.Tables;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rx.Net.StateMachine.EntityFramework.Tests.Tables
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    [Index(nameof(SessionStateId), nameof(Name), IsUnique = true)]
    public class SessionEventAwaiterTable<TContext, TContextKey>
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public Guid AwaiterId { get; set; }
        public Guid SessionStateId { get; set; }
        [Column(TypeName = "varchar(5000)")] public string Name { get; set; }
        [StringLength(256), Column(TypeName = "varchar(256)")] public string Identifier { get; set; }
        [StringLength(256), Column(TypeName = "varchar(256)")] public string? IgnoreIdentifier { get; set; }
        public int SequenceNumber { get; set; }
        public bool IsActive { get; set; } = true;
        public TContextKey ContextId { get; set; }
        public TContext Context { get; set; }
        [ForeignKey(nameof(SessionStateId)), DeleteBehavior(DeleteBehavior.NoAction)]
        public SessionStateTable<TContext, TContextKey> SessionState { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
