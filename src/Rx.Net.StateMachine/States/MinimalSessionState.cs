using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Rx.Net.StateMachine.States
{
    internal class MinimalSessionState
    {
        private static readonly Regex KeyCleanupRegexp = CreateKeyCleanupRegexp();
        private static readonly Regex KeyEnrichRegexp = CreateKeyEnrichRegexp();

        [JsonPropertyName("wf")] public required string WorkflowId { get; set; }
        [JsonPropertyName("s")] public Dictionary<string, object?>? Steps { get; set; }
        [JsonPropertyName("i")] public Dictionary<string, object?>? Items { get; set; }

        public string GetStateString(JsonSerializerOptions jsonSerializerOptions)
        {
            var serializerOptions = new JsonSerializerOptions(jsonSerializerOptions)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault
            };

            var result = JsonSerializer.Serialize(this, serializerOptions);
            result = result.Substring(1, result.Length - 2);
            result = KeyCleanupRegexp.Replace(result, m => $"{m.Groups["name"].Value}:");
            return result;
        }

        public static MinimalSessionState Parse(string stateString, JsonSerializerOptions jsonSerializerOptions)
        {
            stateString = $"{{{stateString}}}";
            stateString = KeyEnrichRegexp.Replace(stateString, m => $"{m.Groups["prefix"].Value}\"{m.Groups["name"].Value}\":");

            return JsonSerializer.Deserialize<MinimalSessionState>(stateString, jsonSerializerOptions)!;
        }

        [GeneratedRegex("\"(?<name>\\w+)\":")] private static partial Regex CreateKeyCleanupRegexp();
        [GeneratedRegex("(?<prefix>[,{])(?<name>\\w+):")] private static partial Regex CreateKeyEnrichRegexp();
    }
}
