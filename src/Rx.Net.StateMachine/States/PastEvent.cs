using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rx.Net.StateMachine.States
{
    public class PastSessionEvent
    {
        public int SequenceNumber { get; }
        public object Event { get; }
        public string EventType { get; }
        public string[] Awaiters { get; }
        public bool Handled { get; }

        [JsonConstructor]
        public PastSessionEvent(int sequenceNumber, object @event, string eventType, string[] awaiters, bool handled)
        {
            SequenceNumber = sequenceNumber;
            Event = @event;
            EventType = eventType;
            Awaiters = awaiters;
            Handled = handled;
        }

        public PastSessionEvent(SessionEvent sessionEvent, JsonSerializerOptions options)
        {
            SequenceNumber = sessionEvent.SequenceNumber;
            var eventType = sessionEvent.Event.GetType();
            Event = sessionEvent.Event;
            EventType = eventType.AssemblyQualifiedName;
            Awaiters = sessionEvent.Awaiters.Select(a => $"{a.AwaiterId:N}-{a.Identifier}-{a.SequenceNumber}").ToArray();
            Handled = sessionEvent.Handled;
        }
    }
}
