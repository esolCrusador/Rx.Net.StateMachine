﻿using System;

namespace Rx.Net.StateMachine.Persistance.Entities
{
    public class SessionEventAwaiterEntity
    {
        public Guid AwaiterId { get; set; }
        public string Name { get; set; }
        public string Identifier { get; set; }
        public int SequenceNumber { get; set; }
    }
}
