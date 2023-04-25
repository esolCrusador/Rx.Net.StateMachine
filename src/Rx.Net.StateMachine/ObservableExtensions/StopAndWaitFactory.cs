using System;
using System.Reactive.Linq;

namespace Rx.Net.StateMachine.ObservableExtensions
{
    public struct StopAndWaitFactory<TSource>
    {
        private IObservable<TSource> _source;
        public StopAndWaitFactory(IObservable<TSource> source)
        {
            _source = source;
        }

        /// <summary>
        /// Waits for event and than persists it
        /// </summary>
        public IObservable<TEvent> For<TEvent>(StateMachineScope scope, string eventStateId, Func<TEvent, bool> filter = null)
        {
            return _source.Select(_ => StateMachineObservableExtensions.StopAndWait(scope, eventStateId, filter))
                .Concat();
        }
    }
}
