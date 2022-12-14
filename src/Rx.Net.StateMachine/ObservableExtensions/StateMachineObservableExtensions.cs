using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.States;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.ObservableExtensions
{
    public static class StateMachineObservableExtensions
    {
        public static IObservable<TSource> IncreaseRecoursionDepth<TSource>(this IObservable<TSource> source, StateMachineScope scope)
        {
            return source.SelectAsync(async s =>
            {
                await scope.IncreaseRecursionDepth();
                return s;
            }).Concat();
        }
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

        public static IObservable<TSource> PersistBeforePrevious<TSource>(this IObservable<TSource> source, StateMachineScope scope, string stateId, TSource defaultValue = default)
        {
            if (scope.TryGetStep<TSource>(stateId, out var stepValue))
                return Of(stepValue);

            return Observable.FromAsync(async () =>
            {
                await scope.AddStep(stateId, defaultValue);
                await source.ToTask();

                return defaultValue;
            });
        }

        public static StopAndWaitFactory<TSource> StopAndWait<TSource>(this IObservable<TSource> source) =>
            new StopAndWaitFactory<TSource>(source);

        public static IObservable<TEvent> StopAndWait<TEvent>(this StateMachineScope scope, string stateId, Func<TEvent, bool> matches = null)
        {
            return StopAndWait<TEvent>(scope, matches).Persist(scope, stateId);
        }

        /// <summary>
        /// Starts await for event or triggers it if it is in events queue.
        /// </summary>
        private static IObservable<TEvent> StopAndWait<TEvent>(StateMachineScope scope, Func<TEvent, bool> matches = null)
        {
            var notHandledEvents = scope.GetEvents(matches).ToList();

            if (notHandledEvents.Count != 0)
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
