namespace Rx.Net.StateMachine.States
{
    public class SessionStateStep
    {
        public int SequenceNumber { get; }
        public string State { get; private set; }
        public SessionStateStep(string state, int sequenceNumber)
        {
            State = state;
            SequenceNumber = sequenceNumber;
        }
    }
}
