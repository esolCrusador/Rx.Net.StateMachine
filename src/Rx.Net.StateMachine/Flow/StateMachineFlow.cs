using System;

namespace Rx.Net.StateMachine.Flow
{
    public class StateMachineFlow<TElement> : IFlow<TElement>
    {
        private readonly StateMachineScope _scope;
        private readonly IObservable<TElement> _observable;

        public StateMachineFlow(StateMachineScope scope, IObservable<TElement> observable)
        {
            _scope = scope;
            _observable = observable;
        }

        IObservable<TElement> IFlow<TElement>.Observable => _observable;

        public StateMachineScope Scope => _scope;
    }
}
