using Rx.Net.StateMachine.Events;
using System;

namespace Rx.Net.StateMachine.States
{
    public class SessionEventAwaiter
    {
        public Guid AwaiterId { get; }
        public string Name { get; }
        public int SequenceNumber { get; }
        public string Identifier { get; }

        public SessionEventAwaiter(string name, IEventAwaiter eventAwaiter, int sequenceNumber)
        {
            Name = name;
            Identifier = eventAwaiter.AwaiterId;
            SequenceNumber = sequenceNumber;
        }

        public SessionEventAwaiter(Guid awaiterId, string name, string awaiterIdentifier, int sequenceNumber)
        {
            AwaiterId = awaiterId;
            Name = name;
            Identifier = awaiterIdentifier;
            SequenceNumber = sequenceNumber;
        }
    }
}
