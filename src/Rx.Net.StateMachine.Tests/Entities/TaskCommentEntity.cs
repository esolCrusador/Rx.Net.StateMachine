using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rx.Net.StateMachine.Tests.Entities
{
    public class TaskCommentEntity
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int CommentId { get; set; }
        public int TaskId { get; set; }
        public Guid UserId { get; set; }
        [StringLength(1024)] public string Text { get; set; }

        [ForeignKey(nameof(TaskId))] public TaskEntity Task { get; set; }
        [ForeignKey(nameof(UserId))] public UserEntity User { get; set; }
    }
}
