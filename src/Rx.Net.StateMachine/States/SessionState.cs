using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Exceptions;
using Rx.Net.StateMachine.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;

namespace Rx.Net.StateMachine.States
{
    public class SessionState
    {
        private readonly Dictionary<string, SessionStateStep> _steps;
        private readonly Dictionary<string, object?> _items;
        private readonly List<PastSessionEvent> _pastEvents;
        private readonly List<SessionEvent> _events;
        private readonly ConcurrentDictionary<string, SessionEventAwaiter> _sessionEventAwaiter;

        public Guid? SessionStateId { get; }
        public string WorkflowId { get; private set; }
        public int Counter { get; private set; }
        public int Version { get; }
        public bool IsDefault { get; private set; }
        public object Context { get; private set; }
        public IReadOnlyDictionary<string, SessionStateStep> Steps => _steps;
        public IReadOnlyDictionary<string, object?> Items => _items;
        public IReadOnlyCollection<PastSessionEvent> PastEvents => _pastEvents;
        public IReadOnlyCollection<SessionEvent> Events => _events;
        public IEnumerable<SessionEventAwaiter> SessionEventAwaiters => _sessionEventAwaiter.Values;

        public bool Changed { get; private set; } = false;

        public SessionStateStatus Status { get; set; }
        public string? Result { get; set; }

        public SessionState(Guid? sessionStateId, string workflowId, object context, bool isDefault, int counter, Dictionary<string, SessionStateStep> steps, Dictionary<string, object?> items, List<PastSessionEvent> pastEvents, List<SessionEventAwaiter> sessionEventAwaiter)
        {
            SessionStateId = sessionStateId;
            WorkflowId = workflowId;
            Context = context;
            IsDefault = isDefault;
            Counter = counter;
            Version = counter;
            _steps = steps;
            _items = items;
            _pastEvents = pastEvents;
            _events = new List<SessionEvent>();
            _sessionEventAwaiter = new ConcurrentDictionary<string, SessionEventAwaiter>(
                sessionEventAwaiter.Select(aw => new KeyValuePair<string, SessionEventAwaiter>(aw.Name, aw))
            );
        }

        public SessionState(string workflowId, object context) : this(null, workflowId, context, false, 0, new Dictionary<string, SessionStateStep>(), new Dictionary<string, object?>(), new List<PastSessionEvent>(), new List<SessionEventAwaiter>())
        {
            Status = SessionStateStatus.InProgress;
        }

        internal bool TryGetStep<TSource>(string stateId, JsonSerializerOptions options, Func<JsonElement, JsonSerializerOptions, TSource>? deserializeOldValue, [MaybeNullWhen(false)] out TSource? source)
        {
            source = default;
            if (!_steps.TryGetValue(stateId, out var step))
                return false;

            Func<JsonElement, JsonSerializerOptions, TSource>? deserializeOldValueAndPersist = deserializeOldValue == null
                ? null
                : (jsonElement, options) =>
                {
                    var value = deserializeOldValue(jsonElement, options);
                    _steps[stateId] = new SessionStateStep(value, step.SequenceNumber);

                    return value;
                };
            source = step.State.DeserializeValue<TSource>(options, deserializeOldValueAndPersist);
            return true;
        }

        internal TStep? GetStep<TStep>(string stateId, JsonSerializerOptions options, Func<JsonElement, JsonSerializerOptions, TStep>? deserializeOldValue)
        {
            if (!TryGetStep<TStep>(stateId, options, deserializeOldValue, out var step))
                throw new StepNotFoundException(stateId);

            return step;
        }

        internal void AddStep<Tsource>(string stateId, Tsource source, JsonSerializerOptions options)
        {
            if (!_steps.TryAdd(stateId, new SessionStateStep(source, GetSequenceNumber())))
                throw new DuplicatedStepException(stateId);
        }

        internal void AddItem<TItem>(string itemId, TItem item, JsonSerializerOptions options)
        {
            if (!_items.TryAdd(itemId, item))
                throw new DuplicatedItemException(itemId);
        }

        internal bool TryUpdateItem<TItem>(string itemId, Func<TItem, TItem> updateAction, JsonSerializerOptions options)
        {
            if (!_items.TryGetValue(itemId, out var itemValue))
                return false;

            var value = itemValue.DeserializeValue<TItem>(options, null)
                ?? throw new InvalidOperationException($"Item {itemId} is null");
            value = updateAction(value);

            _items[itemId] = value;
            return true;
        }

        internal bool TryUpdateItem<TItem>(string itemId, Action<TItem> updateAction, JsonSerializerOptions options)
        {
            if (!_items.TryGetValue(itemId, out var itemValue))
                return false;

            var value = itemValue.DeserializeValue<TItem>(options, null)
                ?? throw new InvalidOperationException($"Item {itemId} is null");
            updateAction(value);

            _items[itemId] = value;
            return true;
        }

        internal void UpdateItem<TItem>(string itemId, Func<TItem?, TItem> updateAction, JsonSerializerOptions options)
        {
            bool updated = TryUpdateItem(itemId, updateAction, options);
            if (!updated)
                throw new ItemNotFoundException(itemId);
        }

        internal void UpdateItem<TItem>(string itemId, TItem item, JsonSerializerOptions options)
        {
            _items[itemId] = item;
        }

        internal void AddOrUpdateItem<TItem>(string itemId, Func<TItem> createAction, Func<TItem, TItem> updateAction, JsonSerializerOptions options)
        {
            if (!TryUpdateItem(itemId, updateAction, options))
                AddItem(itemId, createAction(), options);
        }

        internal void AddOrUpdateItem<TItem>(string itemId, Func<TItem> createAction, Action<TItem> updateAction, JsonSerializerOptions options)
        {
            if (!TryUpdateItem(itemId, updateAction, options))
                AddItem(itemId, createAction(), options);
        }

        internal void AddOrUpdateItem<TItem>(string itemId, TItem item) =>
            _items[itemId] = item;

        internal bool TryGetItem<TItem>(string itemId, JsonSerializerOptions options, [MaybeNullWhen(false)] out TItem? item)
        {
            if (!_items.TryGetValue(itemId, out object? itemValue))
            {
                item = default;
                return false;
            }

            item = itemValue.DeserializeValue<TItem>(options, null);
            return true;
        }

        internal TItem GetItem<TItem>(string itemId, JsonSerializerOptions options)
        {
            if (!TryGetItem<TItem>(itemId, options, out var item))
                throw new ItemNotFoundException(itemId);

            return item;
        }

        internal TItem? GetItemOrDefault<TItem>(string itemId, JsonSerializerOptions options, TItem? defaultItem = default)
        {
            if (!TryGetItem<TItem>(itemId, options, out var item))
                return defaultItem;

            return item;
        }

        internal void DeleteItem(string itemId)
        {
            if (!_items.Remove(itemId))
                throw new ItemNotFoundException(itemId);
        }

        internal void ForceAddEvent(object @event)
        {
            var se = new SessionEvent(@event, GetSequenceNumber(), null);

            _events.Add(se);
        }

        internal bool AddEvent(object @event, IEnumerable<IEventAwaiter> eventAwaiters)
        {
            var awaiterIds = eventAwaiters.Select(ea => ea.AwaiterId).ToHashSet();
            var awaiters = _sessionEventAwaiter.Values.Where(e => awaiterIds.Contains(e.Identifier)).ToArray();
            if (awaiters.Length == 0)
                return false;

            var se = new SessionEvent(@event, GetSequenceNumber(), awaiters);

            _events.Add(se);
            return true;
        }

        internal IEnumerable<TEvent> GetEvents<TEvent>(IEventAwaiter<TEvent> eventAwaiter, Func<TEvent, bool>? filter)
        {
            var result = _events.Where(es => es.Event is TEvent
                && es.Awaiters?.Any(aw =>
                {
                    if (aw.Identifier != eventAwaiter.AwaiterId)
                        return false;

                    if (eventAwaiter is not IEventAwaiterIgnore)
                        return true;

                    var ignoreAwaiter = ((IEventAwaiterIgnore)AwaiterExtensions.CreateAwaiter(eventAwaiter.GetType(), es.Event));

                    return ignoreAwaiter.IgnoreIdentifier != aw.IgnoreIdentifier;
                }) != false)
                .Select(es => (TEvent)es.Event);

            if (filter != null)
                result = result.Where(filter);

            return result;
        }

        internal void AddEventAwaiter<TEvent>(string stateId, IEventAwaiter<TEvent> eventAwaiter)
        {
            _sessionEventAwaiter.TryAdd(stateId, new SessionEventAwaiter(stateId, eventAwaiter, GetSequenceNumber()));
        }

        internal void MakeDefault(bool isDefault)
        {
            IsDefault = isDefault;
        }

        internal void RemoveEventAwaiters(string prefix)
        {
            var keysToRemove = _sessionEventAwaiter.Keys.Where(k => k.StartsWith(prefix));
            foreach (var key in keysToRemove)
                _sessionEventAwaiter.Remove(key, out var _);
        }

        internal void MarkEventAsHandled<TEvent>(TEvent @event, JsonSerializerOptions options)
            where TEvent : class
        {
            var eventObject = (object)@event;
            var eventState = _events.FirstOrDefault(se => se.Event == eventObject);
            if (eventState == null)
                return; // Already moved to past

            eventState.Handled = true;
            _pastEvents.Add(new PastSessionEvent(eventState, options));

            if (eventState.Awaiters != null)
                foreach (var awaiter in eventState.Awaiters)
                    _sessionEventAwaiter.Remove(awaiter.Name, out var _);
            _events.Remove(eventState);
        }

        internal void RemoveNotHandledEvents(JsonSerializerOptions options)
        {
            foreach (var e in _events)
                _pastEvents.Add(new PastSessionEvent(e, options));

            _events.Clear();
        }

        internal MinimalSessionState ToMinimalState() => new MinimalSessionState
        {
            WorkflowId = WorkflowId,
            Steps = _steps.Count == 0 ? null : _steps.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.State),
            Items = _items.Count == 0 ? null : _items,
        };

        private int GetSequenceNumber() => ++Counter;
    }
}
