using System;

namespace Rx.Net.StateMachine.Persistance.Entities
{
    public class SessionEventAwaiterEntity
    {
        public Guid AwaiterId { get; set; }
        public required string Name { get; set; }
        public required string Identifier { get; set; }
        public required string? IgnoreIdentifier {  get; set; }
        public int SequenceNumber { get; set; }
    }
}
