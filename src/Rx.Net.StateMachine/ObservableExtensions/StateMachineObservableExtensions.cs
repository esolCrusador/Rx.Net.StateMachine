using Rx.Net.StateMachine.ObservableExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.ObservableExtensions
{
    public static class StateMachineObservableExtensions
    {
        public static IObservable<TSource> Persist<TSource>(this IObservable<TSource> source, StateMachineScope scope, string stateId)
        {
            if (scope.TryGetStep<TSource>(stateId, out var stepValue))
                return Of(stepValue);

            return source.SelectAsync(async s =>
            {
                await scope.AddStep(stateId, s);

                return s;
            }).Concat();
        }

        public static StopAndWaitFactory<TSource> StopAndWait<TSource>(this IObservable<TSource> source) =>
            new StopAndWaitFactory<TSource>(source);

        public static IObservable<TEvent> StopAndWait<TEvent>(StateMachineScope scope, Func<TEvent, bool> matches = null)
        {
            var notHandledEvents = scope.GetEvents(matches).ToList();

            if(notHandledEvents.Count != 0)
            {
                return notHandledEvents.ToObservable().SelectAsync(async e =>
                {
                    await scope.EventHandled(e);

                    return e;
                }).Concat();
            }

            return Observable.Create<TEvent>(async observer =>
            {
                await scope.AddEventAwaiter<TEvent>();
                observer.OnCompleted();
            });
        }

        public static IObservable<TSource> Of<TSource>(TSource source) =>
            Observable.Create<TSource>(observer =>
            {
                observer.OnNext(source);
                observer.OnCompleted();

                return Task.CompletedTask;
            });
    }
}
