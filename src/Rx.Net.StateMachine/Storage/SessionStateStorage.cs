using Rx.Net.StateMachine.States;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Storage
{
    public class SessionStateStorage : ISessionStateStorage
    {
        private readonly PersistStrategy _persistStrategy;
        private readonly Func<SessionState, Task> _persist;
        private readonly Subject<Unit> _persisted;


        public IObservable<Unit> Persisted => _persisted.Take(1);

        public SessionStateStorage(PersistStrategy persistStrategy, Func<SessionState, Task> persist)
        {
            _persistStrategy = persistStrategy;
            _persist = persist;
            _persisted = new Subject<Unit>();
        }

        public async Task PersistEventAwaiter(SessionState sessionState)
        {
            if (StrategyContains(PersistStrategy.PersistEachAwaiter))
                await _persist(sessionState);

            _persisted.OnNext(Unit.Default);
        }

        public async Task PersistEventState(SessionState sessionState)
        {
            if (StrategyContains(PersistStrategy.PersistEachEvent))
                await _persist(sessionState);

            _persisted.OnNext(Unit.Default);
        }

        public async Task PersistStepState(SessionState sessionState)
        {
            if (StrategyContains(PersistStrategy.PersistEachState))
                await _persist(sessionState);

            _persisted.OnNext(Unit.Default);
        }

        public async Task PersistItemState(SessionState sessionState)
        {
            if (StrategyContains(PersistStrategy.PersistEachItem))
                await _persist(sessionState);

            _persisted.OnNext(Unit.Default);
        }

        public async Task PersistSessionState(SessionState sessionState)
        {
            if (StrategyContains(PersistStrategy.PersistFinally))
                await _persist(sessionState);
            
            _persisted.OnNext(Unit.Default);
        }

        private bool StrategyContains(PersistStrategy persistStrategy) =>
            (_persistStrategy & persistStrategy) == persistStrategy;


        public static readonly SessionStateStorage Empty = new SessionStateStorage(PersistStrategy.Default, ss => Task.CompletedTask);
    }
}
