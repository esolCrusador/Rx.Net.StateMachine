using Rx.Net.StateMachine.States;
using System;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Storage
{
    public class SessionStateStorage : ISessionStateStorage
    {
        private readonly PersistStrategy _persistStrategy;
        private readonly Func<SessionState, Task> _persist;

        public SessionStateStorage(PersistStrategy persistStrategy, Func<SessionState, Task> persist)
        {
            _persistStrategy = persistStrategy;
            _persist = persist;
        }

        public async Task PersistEventAwaiter(SessionState sessionState)
        {
            if (StrategyContains(PersistStrategy.PersistEachAwaiter))
                await _persist(sessionState);
        }

        public async Task PersistEventState(SessionState sessionState)
        {
            if (StrategyContains(PersistStrategy.PersistEachEvent))
                await _persist(sessionState);
        }

        public async Task PersistStepState(SessionState sessionState)
        {
            if (StrategyContains(PersistStrategy.PersistEachState))
                await _persist(sessionState);
        }

        public async Task PersistItemState(SessionState sessionState)
        {
            if (StrategyContains(PersistStrategy.PersistEachItem))
                await _persist(sessionState);
        }

        public async Task PersistSessionState(SessionState sessionState)
        {
            if (StrategyContains(PersistStrategy.PersistFinally))
                await _persist(sessionState);
        }

        private bool StrategyContains(PersistStrategy persistStrategy) =>
            (_persistStrategy & persistStrategy) == persistStrategy;


        public static readonly SessionStateStorage Empty = new SessionStateStorage(PersistStrategy.Default, ss => Task.CompletedTask);
    }
}
