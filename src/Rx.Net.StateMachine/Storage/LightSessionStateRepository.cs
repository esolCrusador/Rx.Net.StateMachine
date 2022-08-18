using Rx.Net.StateMachine.States;
using System;
using System.Collections.Generic;
using System.Text;
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

        public Task PersistEventAwaiter(SessionState sessionState) =>
            StrategyContains(PersistStrategy.PersistEachAwaiter)
                ? _persist(sessionState)
                : Task.CompletedTask;
        public Task PersistEventState(SessionState sessionState) =>
            StrategyContains(PersistStrategy.PersistEachEvent)
                ? _persist(sessionState)
                :Task.CompletedTask;
        public Task PersistStepState(SessionState sessionState) =>
            StrategyContains(PersistStrategy.PersistEachState)
                ? _persist(sessionState)
                :Task.CompletedTask;

        public Task PersistSessionState(SessionState sessionState) =>
            StrategyContains(PersistStrategy.PersistFinally)
                ? _persist(sessionState)
                : Task.CompletedTask;

        private bool StrategyContains(PersistStrategy persistStrategy) =>
            (_persistStrategy & persistStrategy) == persistStrategy;
    }
}
