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
        private readonly Dictionary<string, SessionStateStep> _steps;
        private readonly List<PastSessionEvent> _pastEvents;
        private readonly List<SessionEvent> _events;
        private readonly List<SessionEventAwaiter> _sessionEventAwaiter;

        public string WorkflowId { get; private set; }
        public int Counter { get; private set; }
        public object Context { get; private set; }
        public IReadOnlyDictionary<string, SessionStateStep> Steps => _steps;
        public IReadOnlyCollection<PastSessionEvent> PastEvents => _pastEvents;
        public IReadOnlyCollection<SessionEvent> Events => _events;
        public IReadOnlyCollection<SessionEventAwaiter> SessionEventAwaiters => _sessionEventAwaiter;

        public bool Changed { get; private set; } = false;

        public SessionStateStatus Status { get; set; }
        public string Result { get; set; }

        public SessionState(string workflowId, object context, int counter, Dictionary<string, SessionStateStep> steps, List<PastSessionEvent> pastEvents, List<SessionEventAwaiter> sessionEventAwaiter)
        {
            WorkflowId = workflowId;
            Context = context;
            Counter = counter;
            _steps = steps;
            _pastEvents = pastEvents;
            _events = new List<SessionEvent>();
            _sessionEventAwaiter = sessionEventAwaiter;
        }

        public SessionState(string workflowId, object context) : this(workflowId, context, 0, new Dictionary<string, SessionStateStep>(), new List<PastSessionEvent>(), new List<SessionEventAwaiter>())
        {
            Status = SessionStateStatus.Created;
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

        internal TStep GetStep<TStep>(string stateId, JsonSerializerOptions options)
        {
            if (!TryGetStep<TStep>(stateId, options, out var step))
                throw new StepNotFoundException(stateId);

            return step;
        }

        internal void AddStep<Tsource>(string stateId, Tsource source, JsonSerializerOptions options)
        {
            if (!_steps.TryAdd(stateId, new SessionStateStep(JsonSerializer.Serialize(source, options), GetSequenceNumber())))
                throw new DuplicatedStepException(stateId);
        }

        internal void UpdateStep<TSource>(string stepId, TSource source, JsonSerializerOptions options)
        {
            _steps[stepId].UpdateState(JsonSerializer.Serialize(source, options));
        }

        internal void ForceAddEvent<TEvent>(TEvent @event)
        {
            var se = new SessionEvent(@event, GetSequenceNumber(), new SessionEventAwaiter[0]);

            _events.Add(se);
        }

        internal bool AddEvent<TEvent>(TEvent @event)
        {
            var eventTypeId = @event.GetType().GUID;
            var awaiters = _sessionEventAwaiter.Where(e => e.Type.GUID == eventTypeId).ToArray();
            if (awaiters.Length == 0)
                return false;

            var se = new SessionEvent(@event, GetSequenceNumber(), awaiters);

            _events.Add(se);
            return true;
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

        internal MinimalSessionState ToMinimalState() => new MinimalSessionState
        {
            WorkflowId = WorkflowId,
            Steps = _steps,
            Counter = Counter
        };

        private int GetSequenceNumber() => ++Counter;
    }
}
