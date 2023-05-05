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

        /// <summary>
        /// Waits for event and than persists it
        /// </summary>
        public IObservable<TEvent> For<TEvent>(StateMachineScope scope, string eventStateId, Func<TEvent, TSource, bool> filter)
        {
            if(filter == null)
                throw new ArgumentNullException(nameof(filter));

            return _source.Select(s =>
            {
                return StateMachineObservableExtensions.StopAndWait<TEvent>(scope, eventStateId, e => filter(e, s));
            }).Concat();
        }
    }
}
