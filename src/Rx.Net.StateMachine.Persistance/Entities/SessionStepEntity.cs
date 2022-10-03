namespace Rx.Net.StateMachine.Persistance.Entities
{
    public class SessionStepEntity
    {
        public string Id { get; set; }
        public string State { get; set; }
        public int SequenceNumber { get; set; }
    }
}
