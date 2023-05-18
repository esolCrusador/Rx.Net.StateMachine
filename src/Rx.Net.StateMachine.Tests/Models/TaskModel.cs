using System;
using System.Collections.Generic;

namespace Rx.Net.StateMachine.Tests.Models
{
    public class TaskModel
    {
        public int TaskId { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public Guid AssigneeId { get; set; }
        public Guid? SupervisorId { get; set; }
        public TaskState State { get; set; }
        public List<CommentModel> Comments { get; set; }
    }
}
