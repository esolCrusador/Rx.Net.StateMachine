using Rx.Net.StateMachine.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Rx.Net.StateMachine.States
{
    public class SessionState
    {
        public int Counter { get; private set; }
        private readonly Dictionary<string, SessionStateStep> _steps;
        private readonly List<PastSessionEvent> _pastEvents;
        private readonly List<SessionEvent> _events;
        private readonly List<SessionEventAwaiter> _sessionEventAwaiter;

        public IReadOnlyDictionary<string, SessionStateStep> Steps => _steps;
        public IReadOnlyCollection<PastSessionEvent> PastEvents => _pastEvents;
        public IReadOnlyCollection<SessionEvent> Events => _events;
        public IReadOnlyCollection<SessionEventAwaiter> SessionEventAwaiters => _sessionEventAwaiter;

        public bool Changed { get; private set; } = false;

        public SessionStateStatus Status { get; set; }
        public string Result { get; set; }

        public SessionState(int counter, Dictionary<string, SessionStateStep> steps, List<PastSessionEvent> pastEvents, List<SessionEventAwaiter> sessionEventAwaiter)
        {
            Counter = counter;
            _steps = steps;
            _pastEvents = pastEvents;
            _events = new List<SessionEvent>();
            _sessionEventAwaiter = sessionEventAwaiter;
        }

        public SessionState() : this(0, new Dictionary<string, SessionStateStep>(), new List<PastSessionEvent>(), new List<SessionEventAwaiter>())
        {
            Status = SessionStateStatus.Created;
            Counter = 0;
        }

        internal bool TryGetStep<TSource>(string stateId, JsonSerializerOptions options, out TSource source)
        {
            source = default;
            if (!_steps.TryGetValue(stateId, out var step))
                return false;

            string stepState = step.State;
            source = JsonSerializer.Deserialize<TSource>(stepState, options);
            return true;
        }

        internal void AddStep<Tsource>(string stateId, Tsource source, JsonSerializerOptions options)
        {
            if (!_steps.TryAdd(stateId, new SessionStateStep(JsonSerializer.Serialize(source, options), GetSequenceNumber())))
                throw new DuplicatedStepException(stateId);
        }

        internal void AddEvent<TEvent>(TEvent @event)
        {
            var eventTypeId = @event.GetType().GUID;
            var awaiters = _sessionEventAwaiter.Where(e => e.Type.GUID == eventTypeId).ToArray();
            if (awaiters.Length == 0)
                return;

            var se = new SessionEvent(@event, GetSequenceNumber(), awaiters);

            _events.Add(se);
        }

        internal IEnumerable<TEvent> GetEvents<TEvent>(Func<TEvent, bool> filter, JsonSerializerOptions options)
        {
            var result = _events.Select(es => es.Event).OfType<TEvent>();

            if (filter != null)
                result = result.Where(filter);

            return result;
        }

        internal void AddEventAwaiter<TEvent>()
        {
            _sessionEventAwaiter.Add(new SessionEventAwaiter(typeof(TEvent), GetSequenceNumber()));
        }

        internal void MarkEventAsHandled<TEvent>(TEvent @event, JsonSerializerOptions options)
        {
            var eventObject = (object)@event;
            var eventState = _events.FirstOrDefault(se => se.Event == eventObject);
            if (eventState == null)
                return; // Already moved to past

            eventState.Handled = true;
            _pastEvents.Add(new PastSessionEvent(eventState, options));

            foreach (var awaiter in eventState.Awaiters)
                _sessionEventAwaiter.Remove(awaiter);
            _events.Remove(eventState);
        }

        internal void RemoveNotHandledEvents(JsonSerializerOptions options)
        {
            foreach (var e in _events)
                _pastEvents.Add(new PastSessionEvent(e, options));

            _events.Clear();
        }

        internal void SetResult(object result, JsonSerializerOptions options)
        {
            Result = JsonSerializer.Serialize(result, options);
        }

        private int GetSequenceNumber() => ++Counter;
    }
}
