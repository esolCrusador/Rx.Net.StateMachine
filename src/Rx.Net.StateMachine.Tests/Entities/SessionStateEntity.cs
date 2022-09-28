using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Tests.Entities;
using System;
using System.Collections.Generic;

namespace Rx.Net.StateMachine.Tests
{
    public partial class WorkflowManager
    {
        public class SessionStateEntity
        {
            public Guid SessionId { get; set; }
            public Guid UserId { get; set; }

            public string WorkflowId { get; set; }
            public int Counter { get; set; }
            public List<SessionStepEntity> Steps { get; set; }
            public List<SessionItemEntity> Items { get; set; }
            public List<SessionEventEntity> PastEvents { get; set; }
            public List<SessionEventAwaiterEntity> Awaiters { get; set; }
            public SessionStateStatus Status { get; set; }
            public string Result { get; set; }
        }
    }
}
