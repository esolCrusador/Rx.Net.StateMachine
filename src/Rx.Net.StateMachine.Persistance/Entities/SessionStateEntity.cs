using Rx.Net.StateMachine.States;
using System;
using System.Collections.Generic;

namespace Rx.Net.StateMachine.Persistance.Entities
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public sealed class SessionStateEntity
    {
        public Guid SessionStateId { get; set; }
        public string WorkflowId { get; set; }
        public int Counter { get; set; }
        public bool IsDefault { get; set; }
        public List<SessionStepEntity> Steps { get; set; }
        public List<SessionItemEntity> Items { get; set; }
        public List<SessionEventEntity> PastEvents { get; set; }
        public List<SessionEventAwaiterEntity> Awaiters { get; set; }
        public SessionStateStatus Status { get; set; }
        public object Context { get; set; }
        public string? Result { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
