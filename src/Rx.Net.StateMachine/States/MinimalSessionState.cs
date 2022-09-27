using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Rx.Net.StateMachine.States
{
    internal class MinimalSessionState
    {
        [JsonPropertyName("wf")] public string WorkflowId { get; set; }
        [JsonPropertyName("s")] public Dictionary<string, SessionStateStep> Steps { get; set; }
        [JsonPropertyName("c")] public int Counter { get; set; }
    }
}
