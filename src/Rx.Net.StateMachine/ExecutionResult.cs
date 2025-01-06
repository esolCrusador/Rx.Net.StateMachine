namespace Rx.Net.StateMachine
{
    public struct ExecutionResult
    {
        public required bool IsFinished {  get; set; }
        public string? Result {  get; set; }
    }
}
