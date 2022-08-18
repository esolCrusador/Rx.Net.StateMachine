using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;

namespace Rx.Net.StateMachine.ObservableExtensions
{
    public struct StopAndWaitFactory<TSource>
    {
        private IObservable<TSource> _source;
        public StopAndWaitFactory(IObservable<TSource> source)
        {
            _source = source;
        }

        public IObservable<TEvent> For<TEvent>(StateMachineScope scope, Func<TEvent, bool> filter = null)
        {
            return _source.Select(_ => StateMachineObservableExtensions.StopAndWait(scope, filter))
                .Concat();
        }
    }
}
