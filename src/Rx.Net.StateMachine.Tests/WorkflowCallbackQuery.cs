using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Rx.Net.StateMachine.Tests
{
    public class WorkflowCallbackQuery
    {
        private Dictionary<string, string>? _parameters;

        public string? WorkflowFactoryId { get; set; }
        public Guid? SessionId { get; set; }
        public string? Command { get; set; }
        public Dictionary<string, string> Parameters { get => _parameters ??= new Dictionary<string, string>(); set => _parameters = value; }

        public override string ToString()
        {
            var result = new StringBuilder();
            if (WorkflowFactoryId != null)
                result.Append($"f:{WorkflowFactoryId}");
            if (SessionId != null)
            {
                if (result.Length > 0)
                    result.Append(',');
                result.Append($"s:{SessionId:n}");
            }
            if (Command != null)
            {
                if (result.Length > 0)
                    result.Append(',');
                result.Append($"c:{Command}");
            }

            if (_parameters?.Count > 0)
            {
                if (result.Length > 0)
                    result.Append(',');
                result.Append($"p:{string.Join(";", _parameters.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");
            }

            if (result.Length > 100)
                throw new InvalidDataException($"Callback query data length must be leee then 100 chars but was {result.Length}");

            return result.ToString();
        }

        public static WorkflowCallbackQuery Parse(string data)
        {
            var workflowFactoryId = GetValue(data, "f:", 0, out var to);
            var sessionId = GetValue(data, "s:", to, out to);
            var command = GetValue(data, "c:", to, out to);
            var parametersString = GetValue(data, "p:", to, out to);
            var result = new WorkflowCallbackQuery
            {
                Command = command,
                WorkflowFactoryId = workflowFactoryId,
                SessionId = sessionId == null ? null : Guid.Parse(sessionId),
            };
            if (parametersString != null)
                result.Parameters = parametersString.Split(';').Select(kvp =>
                {
                    var spliter = kvp.IndexOf(":");
                    return new KeyValuePair<string, string>(kvp.Substring(0, spliter), kvp.Substring(spliter + 1));
                }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return result;
        }

        public static bool TryParse(string data, out WorkflowCallbackQuery result)
        {
            result = Parse(data);
            if (result.WorkflowFactoryId == null && result.SessionId == null && result.Command == null && result.Parameters == null)
                return false;

            return true;
        }

        private static string? GetValue(string data, string key, int from, out int to)
        {
            to = from;
            if (from > data.Length)
                return null;
            var keyIndex = data.IndexOf(key, from);
            if (keyIndex == -1)
                return null;
            if (keyIndex != from)
                throw new FormatException();

            var keyLength = key.Length;
            to = data.IndexOf(',', from);
            if (to == -1)
                to = data.Length + 1;
            else
                to += 1;

            return data.Substring(from + keyLength, to - from - keyLength - 1);
        }
    }
}
