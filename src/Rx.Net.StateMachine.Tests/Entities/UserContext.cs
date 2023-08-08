using Rx.Net.StateMachine.EntityFramework.Tables;
using Rx.Net.StateMachine.Tests.Entities;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rx.Net.StateMachine.Tests.Persistence
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public class UserContext
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int ContextId { get; set; }
        public Guid UserId { get; set; }
        public long ChatId { get; set; }
        public long BotId { get; set; }
        [StringLength(128)] public string Name { get; set; }
        [StringLength(64)] public string Username { get; set; }

        [ForeignKey(nameof(UserId))] public UserEntity User { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
