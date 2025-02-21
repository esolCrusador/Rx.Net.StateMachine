namespace Rx.Net.StateMachine.States
{
    public class SessionStateStep
    {
        public int SequenceNumber { get; }
        public object? State { get; private set; }
        public SessionStateStep(object? state, int sequenceNumber)
        {
            State = state;
            SequenceNumber = sequenceNumber;
        }
    }
}
