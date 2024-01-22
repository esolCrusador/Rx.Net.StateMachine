using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Flow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace Rx.Net.StateMachine.Flow
{
    public static class FlowExtensions
    {
        public static IFlow<TElement> Persist<TElement>(this IFlow<TElement> flow, string stateId)
        {
            if (flow.Scope.TryGetStep<TElement>(stateId, out var stepValue))
                return flow.Scope.StartFlow(stepValue!);

            return flow.SelectAsync(async s =>
            {
                await flow.Scope.AddStep(stateId, s);

                return s;
            });
        }

        public static IFlow<TElement?> PersistBeforePrevious<TElement>(this IFlow<TElement> flow, string stateId, TElement? defaultValue = default)
        {
            if (flow.Scope.TryGetStep<TElement>(stateId, out var stepValue))
                return flow.Scope.StartFlow((TElement?)stepValue);

            var observable = Observable.FromAsync(async () =>
            {
                await flow.Scope.AddStep(stateId, defaultValue);
                await flow.Observable.ToTask();

                return defaultValue;
            });

            return new StateMachineFlow<TElement?>(flow.Scope, observable);
        }

        public static StopAndWaitFactory<TSource> StopAndWait<TSource>(this IFlow<TSource> source) =>
            new StopAndWaitFactory<TSource>(source);

        public static IFlow<TEvent> StopAndWait<TEvent>(this StateMachineScope scope, string stateId, IEventAwaiter<TEvent> eventAwaiter, Func<TEvent, bool>? matches = null)
            where TEvent : class
        {
            return WaitOrHandle<TEvent>(scope, stateId, eventAwaiter, matches).Persist(stateId);
        }

        public static IFlow<TResult> WhenAny<TSource, TResult>(this IFlow<TSource> source, string name, params Func<IFlow<TSource>, IFlow<TResult>?>[] threads)
        {
            var whenAnyScope = source.Scope.BeginScope(name);
            return new StateMachineFlow<TResult>(source.Scope, source.Observable.Select(s =>
            {
                var results = threads.Select(obs => obs(whenAnyScope.StartFlow(s))).Where(f => f != null).Select(r => r!.Observable);
                return Observable.Merge(results);
            }).Concat().Take(1)
                .Select(async r =>
                {
                    await whenAnyScope.RemoveScopeAwaiters();
                    return r;
                }).Concat()
            );
        }

        public static IFlow<TResult> WhenAny<TResult>(this StateMachineScope scope, string name, IEnumerable<Func<StateMachineScope, IFlow<TResult>?>> factories)
        {
            var whenAnyScope = scope.BeginScope(name);
            return new StateMachineFlow<TResult>(scope, Observable.Merge(factories.Select(f => f(whenAnyScope)).Where(flow => flow != null).Select(flow => flow!.Observable)).Take(1)
                .Select(async result =>
                {
                    await whenAnyScope.RemoveScopeAwaiters();
                    return result;
                }).Concat()
            );
        }

        public static IFlow<TResult> WhenAny<TResult>(this StateMachineScope scope, string name, params Func<StateMachineScope, IFlow<TResult>?>[] observables)
        {
            return WhenAny(scope, name, (IEnumerable<Func<StateMachineScope, IFlow<TResult>?>>)observables);
        }

        public static IFlow<IList<TResult>> WhenAll<TResult>(this StateMachineScope scope, IEnumerable<IFlow<TResult>> flows)
        {
            return new StateMachineFlow<IList<TResult>>(scope,
                Observable.CombineLatest(flows.Select(fl => fl.Observable.Take(1)))
            );
        }

        public static IFlow<IList<TResult>> WhenAll<TResult>(this StateMachineScope scope, params IFlow<TResult>[] flows)
        {
            return WhenAll(scope, (IEnumerable<IFlow<TResult>>)flows);
        }

        public static IFlow<IList<TResult>> WhenAll<TSource, TResult>(this IFlow<TSource> source, params Func<IFlow<TSource>, IFlow<TResult>>[] flowFactories)
        {
            return new StateMachineFlow<IList<TResult>>(source.Scope,
                source.Observable.Select(element => Observable.CombineLatest(flowFactories.Select(obsFactory => obsFactory(source.Scope.StartFlow(element)).Observable.Take(1))))
                .Concat()
            );
        }

        public static IFlow<IList<TResult>> WhenAll<TSource, TResult>(this IFlow<TSource> source, Func<TSource, StateMachineScope, IEnumerable<IFlow<TResult>>> flowsFactory)
        {
            return new StateMachineFlow<IList<TResult>>(source.Scope,
                source.Observable.Select(element => Observable.CombineLatest(flowsFactory(element, source.Scope).Select(flow => flow.Observable.Take(1))))
                .Concat()
            );
        }

        public static IFlow<IList<TResult>> WhenAll<TSource, TResult>(this IFlow<TSource> source, IEnumerable<IFlow<TResult>> flows)
        {
            return new StateMachineFlow<IList<TResult>>(source.Scope,
                source.Observable.Select(element => Observable.CombineLatest(flows.Select(flow => flow.Observable.Take(1))))
                .Concat()
            );
        }

        public static IFlow<TSource> IncreaseRecoursionDepth<TSource>(this IFlow<TSource> source)
        {
            return source.SelectAsync(async (s, scope) =>
            {
                await scope.IncreaseRecursionDepth();
                return s;
            });
        }

        public static IFlow<TSource> BeginScope<TSource>(this IFlow<TSource> source, string prefix)
        {
            return new StateMachineFlow<TSource>(source.Scope.BeginScope(prefix), source.Observable);
        }

        public static IFlow<TSource> EndScope<TSource>(this IFlow<TSource> source, string prefix)
        {
            return new StateMachineFlow<TSource>(source.Scope.EndScope(prefix), source.Observable);
        }

        private static IFlow<TEvent> WaitOrHandle<TEvent>(StateMachineScope scope, string stateId, IEventAwaiter<TEvent> eventAwaiter, Func<TEvent, bool>? matches)
            where TEvent : class
        {
            var notHandledEvents = scope.GetEvents(eventAwaiter, matches).ToList();

            if (notHandledEvents.Count != 0)
            {
                return new StateMachineFlow<TEvent>(scope, notHandledEvents.ToObservable().Select(async e =>
                {
                    await scope.EventHandled(e);

                    return e;
                }).Concat());
            }

            return new StateMachineFlow<TEvent>(scope, Observable.Create<TEvent>(async observer =>
            {
                await scope.AddEventAwaiter<TEvent>(stateId, eventAwaiter);
                observer.OnCompleted();
            }));
        }
    }
}
