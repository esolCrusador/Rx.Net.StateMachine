using Rx.Net.StateMachine.Exceptions;
using Rx.Net.StateMachine.Helpers;
using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq.Expressions;
using System.Reactive;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine
{
    public struct StateMachineScope
    {
        public string StatePrefix { get; }
        public StateMachine StateMachine { get; }
        public SessionState SessionState { get; }
        public ISessionStateStorage SessionStateStorage { get; }

        public IObservable<Unit> Persisted => SessionStateStorage.Persisted;

        public StateMachineScope(StateMachine stateMachine, SessionState sessionState, ISessionStateStorage sessionStateRepository, string prefix = null)
        {
            StateMachine = stateMachine;
            SessionState = sessionState;
            SessionStateStorage = sessionStateRepository;
            StatePrefix = prefix;
        }

        public bool TryGetStep<TSource>(string stateId, out TSource stepValue) =>
            SessionState.TryGetStep(AddPrefix(stateId), StateMachine.SerializerOptions, out stepValue);

        public StateMachineScope BeginScope(string prefix) =>
            new StateMachineScope(StateMachine, SessionState, SessionStateStorage, AddPrefix(prefix));

        public async Task<StateMachineScope> BeginRecursiveScope(string prefix)
        {
            if (!SessionState.TryGetItem(GetDepthName(prefix), StateMachine.SerializerOptions, out int depth))
            {
                depth = 1;
                await AddItem(GetDepthName(prefix), depth);
            }

            return new StateMachineScope(StateMachine, SessionState, SessionStateStorage, AddPrefix(prefix));
        }

        public async Task<StateMachineScope> IncreaseRecursionDepth()
        {
            int depth = GetRecoursionDepth() ?? throw new StepNotFoundException(GetDepthName(StatePrefix));
            depth++;
            await UpdateItem(GetDepthName(StatePrefix), depth);

            return this;
        }

        public IEnumerable<TEvent> GetEvents<TEvent>(Func<TEvent, bool> matches) =>
            SessionState.GetEvents(matches, StateMachine.SerializerOptions);

        public Task AddStep<TState>(string stateId, TState stepState)
        {
            SessionState.AddStep(AddPrefix(stateId), stepState, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistStepState(SessionState);
        }

        public Task AddItem<TItem>(string itemId, TItem item)
        {
            SessionState.AddItem(AddPrefix(itemId), item, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public Task UpdateItem<TItem>(string itemId, TItem item)
        {
            SessionState.UpdateItem(itemId, item, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public Task AddEvent<TEvent>(TEvent @event)
        {
            SessionState.AddEvent<TEvent>(@event);

            return SessionStateStorage.PersistEventState(SessionState);
        }

        public Task EventHandled<TEvent>(TEvent e)
        {
            SessionState.MarkEventAsHandled(e, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistEventState(SessionState);
        }

        public Task AddEventAwaiter<TEvent>()
        {
            SessionState.AddEventAwaiter<TEvent>();

            return SessionStateStorage.PersistEventAwaiter(SessionState);
        }

        public TContext GetContext<TContext>() => (TContext)SessionState.Context;

        public string GetStateString()
        {
            using var stateStream = new MemoryStream();
            JsonSerializer.Serialize(stateStream, SessionState.ToMinimalState(), StateMachine.SerializerOptions);

            return CompressionHelper.Zip(stateStream);
        }

        private static string GetDepthName(string prefix) => $"{prefix}[depth]";

        private int? GetRecoursionDepth()
        {
            string depthName = GetDepthName(StatePrefix);
            if (!SessionState.TryGetItem<int>(depthName, StateMachine.SerializerOptions, out var depth))
                return null;

            return depth;
        }

        private string AddPrefix(string stateId)
        {
            if (StatePrefix == null)
                return stateId;

            int? depth = GetRecoursionDepth();
            if (depth == null)
                return $"{StatePrefix}.{stateId}";

            return $"{StatePrefix}-{depth}.{stateId}";
        }
    }
}
