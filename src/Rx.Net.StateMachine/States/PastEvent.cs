using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rx.Net.StateMachine.States
{
    public class PastSessionEvent
    {
        public int SequenceNumber { get; }
        public string SerializedEvent { get; }
        public string EventType { get; }
        public string[] Awaiters { get; }
        public bool Handled { get; }

        [JsonConstructor]
        public PastSessionEvent(int sequenceNumber, string serializedEvent, string eventType, string[] awaiters, bool handled)
        {
            SequenceNumber = sequenceNumber;
            SerializedEvent = serializedEvent;
            EventType = eventType;
            Awaiters = awaiters;
            Handled = handled;
        }

        public PastSessionEvent(SessionEvent sessionEvent, JsonSerializerOptions options)
        {
            SequenceNumber = sessionEvent.SequenceNumber;
            var eventType = sessionEvent.Event.GetType();
            SerializedEvent = JsonSerializer.Serialize(sessionEvent.Event, eventType, options);
            EventType = eventType.AssemblyQualifiedName;
            Awaiters = sessionEvent.Awaiters.Select(a => $"{a.AwaiterId:N}-{a.Type.FullName}-{a.SequenceNumber}").ToArray();
            Handled = sessionEvent.Handled;
        }
    }
}
