using Rx.Net.StateMachine.States;
using System;
using System.Collections.Generic;

namespace Rx.Net.StateMachine.Persistance.Entities
{
    public abstract class SessionStateBaseEntity
    {
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
