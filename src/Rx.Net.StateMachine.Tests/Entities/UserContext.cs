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
    }
}
