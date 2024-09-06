namespace Rx.Net.StateMachine.Persistance
{
    public class StateMachineConfiguration
    {
        /// <summary>
        /// Sets how many sessions found for one event can be handled in parallel
        /// </summary>
        public int EventHandlingParallelism { get; set; } = int.MaxValue;
    }
}
