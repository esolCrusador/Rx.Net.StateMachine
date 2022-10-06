using Rx.Net.StateMachine.Persistance.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Rx.Net.StateMachine.Tests
{
    public class TestSessionStateEntity : SessionStateBaseEntity
    {
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
    }
}
