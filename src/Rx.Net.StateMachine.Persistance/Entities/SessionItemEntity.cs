namespace Rx.Net.StateMachine.Persistance.Entities
{
    public class SessionItemEntity
    {
        public required string Id { get; set; }
        public object? Value { get; set; }
    }
}
