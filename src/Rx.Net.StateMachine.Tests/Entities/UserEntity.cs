using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rx.Net.StateMachine.Tests.Entities
{
    public class UserEntity
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public Guid UserId { get; set; }
        public string Name { get; set; }

        public ICollection<UserContext> Contexts { get; set; }
        public ICollection<TaskEntity> AssignedTasks { get; set; }
        public ICollection<TaskEntity> SupervisedTasks { get; set; }
        public ICollection<TaskCommentEntity> Comments { get; set; }
    }
}
