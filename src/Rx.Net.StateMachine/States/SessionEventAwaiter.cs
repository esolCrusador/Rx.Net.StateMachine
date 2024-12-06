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
        public string? IgnoreIdentifier { get; }
        public SessionEventAwaiter(string name, IEventAwaiter eventAwaiter, int sequenceNumber)
        {
            Name = name;
            Identifier = eventAwaiter.AwaiterId;
            SequenceNumber = sequenceNumber;
            if (eventAwaiter is IEventAwaiterIgnore eventAwaiterIgnore)
                IgnoreIdentifier = eventAwaiterIgnore.IgnoreIdentifier;
        }

        public SessionEventAwaiter(Guid awaiterId, string name, string awaiterIdentifier, string? ignoreIdentifier, int sequenceNumber)
        {
            AwaiterId = awaiterId;
            Name = name;
            Identifier = awaiterIdentifier;
            IgnoreIdentifier = ignoreIdentifier;
            SequenceNumber = sequenceNumber;
        }
    }
}
