using System;
using System.Collections.Generic;

namespace Rx.Net.StateMachine.Tests.Events
{
    public class TaskCommentAdded
    {
        public int TaskId { get; set; }
        public int CommentId { get; set; }
        public string Text { get; set; }
        public Dictionary<string, string>? Context { get; set; }
        public Guid UserId { get; set; }
    }
}
