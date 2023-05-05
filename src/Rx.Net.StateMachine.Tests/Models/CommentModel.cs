using System;

namespace Rx.Net.StateMachine.Tests.Models
{
    public class CommentModel
    {
        public int CommentId { get; set; }
        public string Text { get; set; }
        public Guid UserId { get; set; }
    }
}
