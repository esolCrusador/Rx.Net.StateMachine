using Rx.Net.StateMachine.Tests.Models;
using System.Collections.Generic;

namespace Rx.Net.StateMachine.Tests.Events
{
    public class TaskStateChanged
    {
        public int TaskId { get; set; }
        public TaskState State { get; set; }
        public Dictionary<string, string>? Context { get; set; }
    }
}
