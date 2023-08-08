using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rx.Net.StateMachine.Tests.Entities
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public class TaskCommentEntity
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int CommentId { get; set; }
        public int TaskId { get; set; }
        public Guid UserId { get; set; }
        [StringLength(1024)] public string Text { get; set; }

        [ForeignKey(nameof(TaskId))] public TaskEntity Task { get; set; }
        [ForeignKey(nameof(UserId))] public UserEntity User { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
