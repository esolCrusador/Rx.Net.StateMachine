namespace Rx.Net.StateMachine.Persistance.Entities
{
    public class SessionStepEntity
    {
        public required string Id { get; set; }
        public object? State { get; set; }
        public int SequenceNumber { get; set; }
    }
}
