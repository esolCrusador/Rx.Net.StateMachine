using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Storage;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine
{
    public struct StateMachineScope
    {
        public string StatePrefix { get; }
        public StateMachine StateMachine { get; }
        public SessionState SessionState { get; }
        public ISessionStateStorage SessionStateStorage { get; }

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

        public IEnumerable<TEvent> GetEvents<TEvent>(Func<TEvent, bool> matches) =>
            SessionState.GetEvents(matches, StateMachine.SerializerOptions);

        public Task AddStep<TState>(string stateId, TState stepState)
        {
            SessionState.AddStep(AddPrefix(stateId), stepState, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistStepState(SessionState);
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

        private string AddPrefix(string stateId) =>
            StatePrefix == null ? stateId : $"{StatePrefix}.{stateId}";
    }
}
