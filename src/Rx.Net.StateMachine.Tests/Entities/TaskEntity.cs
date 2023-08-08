using Rx.Net.StateMachine.Tests.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rx.Net.StateMachine.Tests.Entities
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public class TaskEntity
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int TaskId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public TaskState State { get; set; }
        public Guid AssigneeId { get; set; }
        public Guid? SupervisorId { get; set; }

        public ICollection<TaskCommentEntity> Comments { get; set; }
        [ForeignKey(nameof(AssigneeId))] public UserEntity Assignee { get; set; }
        [ForeignKey(nameof(SupervisorId))] public UserEntity? Supervisor { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
