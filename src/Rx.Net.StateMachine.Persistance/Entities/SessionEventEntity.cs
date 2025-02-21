namespace Rx.Net.StateMachine.Persistance.Entities
{
    public class SessionEventEntity
    {
        public required object Event { get; set; }
        public required string EventType { get; set; }
        public bool Handled { get; set; }
        public int SequenceNumber { get; set; }
        public required string[] Awaiters { get; set; }
    }
}
