using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Rx.Net.StateMachine.States
{
    internal class MinimalSessionState
    {
        [JsonPropertyName("wf")] public required string WorkflowId { get; set; }
        [JsonPropertyName("s")] public Dictionary<string, SessionStateStep>? Steps { get; set; }
        [JsonPropertyName("i")] public Dictionary<string, object?>? Items { get; set; }
        [JsonPropertyName("c")] public int Counter { get; set; } = 1;
    }
}
