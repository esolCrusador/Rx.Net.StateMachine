using Rx.Net.StateMachine.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
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
            });
        }
        public static IObservable<TSource> Persist<TSource>(this IObservable<TSource> source, StateMachineScope scope, string stateId)
        {
            if (scope.TryGetStep<TSource>(stateId, out var stepValue))
                return Of(stepValue);

            return source.SelectAsync(async s =>
            {
                await scope.AddStep(stateId, s);

                return s;
            });
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

        public static IObservable<TEvent> StopAndWait<TEvent>(this StateMachineScope scope, string stateId, IEventAwaiter<TEvent> eventAwaiter, Func<TEvent, bool> matches = null)
            where TEvent: class
        {
            return WaitOrHandle<TEvent>(scope, stateId, eventAwaiter, matches).Persist(scope, stateId);
        }

        public static IObservable<TResult> WhenAny<TResult>(this StateMachineScope scope, string name, params Func<StateMachineScope, IObservable<TResult>>[] observables)
        {
            var whenAnyScope = scope.BeginScope(name);
            return Observable.Merge(observables.Select(obs => obs(whenAnyScope))).Take(1)
                .TapAsync(() => whenAnyScope.RemoveScopeAwaiters());
        }

        public static IObservable<IList<TResult>> WhenAll<TSource, TResult>(this IObservable<TSource> source, params Func<TSource, IObservable<TResult>>[] observables)
        {
            return source.Select(element => Observable.CombineLatest(observables.Select(obsFactory => obsFactory(element).Take(1))))
                .Concat();
        }

        public static IObservable<TResult> Loop<TResult>(this StateMachineScope scope, string prefix, Func<StateMachineScope, IObservable<TResult>> iteration, Func<TResult, bool>? exit = null)
        {
            return Observable.FromAsync(async () =>
                LoopIteration(await scope.BeginRecursiveScope(prefix), iteration, exit)
            ).Concat();
        }

        private static IObservable<TResult> LoopIteration<TResult>(StateMachineScope scope, Func<StateMachineScope, IObservable<TResult>> iteration, Func<TResult, bool>? exit = null)
        {
            return iteration(scope).Select(r =>
            {
                if (exit?.Invoke(r) == true)
                    return Observable.Start(() => r);

                return Observable.FromAsync(() => scope.IncreaseRecursionDepth())
                    .Select(_ => LoopIteration(scope, iteration, exit))
                    .Concat();
            }).Concat();
        }

        /// <summary>
        /// Starts await for event or triggers it if it is in events queue.
        /// </summary>
        private static IObservable<TEvent> WaitOrHandle<TEvent>(StateMachineScope scope, string stateId, IEventAwaiter<TEvent> eventAwaiter, Func<TEvent, bool> matches)
            where TEvent: class
        {
            var notHandledEvents = scope.GetEvents(matches).ToList();

            if (notHandledEvents.Count != 0)
            {
                return notHandledEvents.ToObservable().SelectAsync(async e =>
                {
                    await scope.EventHandled(e);

                    return e;
                });
            }

            return Observable.Create<TEvent>(async observer =>
            {
                await scope.AddEventAwaiter<TEvent>(stateId, eventAwaiter);
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
