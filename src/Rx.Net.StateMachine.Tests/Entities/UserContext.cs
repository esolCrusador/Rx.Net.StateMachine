using Rx.Net.StateMachine.EntityFramework.Tables;
using Rx.Net.StateMachine.Tests.Entities;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rx.Net.StateMachine.Tests.Persistence
{
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
}
