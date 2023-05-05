using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace Rx.Net.StateMachine.ObservableExtensions
{
    public static class RichObservableExtensions
    {

        public static IObservable<TSource> Tap<TSource>(this IObservable<TSource> source, Action<TSource> execute)
        {
            return source.Select(s =>
            {
                execute(s);
                return s;
            });
        }

        public static IObservable<Unit> MapToVoid<TSource>(this IObservable<TSource> source)
        {
            return source.Select(s => Unit.Default);
        }

        public static IObservable<TResult> MapTo<TSource, TResult>(this IObservable<TSource> source, TResult result)
        {
            return source.Select(s => result);
        }

        public static IObservable<TResult> WhenAny<TSource, TResult>(this IObservable<TSource> source, StateMachineScope scope, string name, params Func<TSource, StateMachineScope, IObservable<TResult>>[] observables)
        {
            var whenAnyScope = scope.BeginScope(name);
            return source.Select(s => Observable.Merge(observables.Select(obs => obs(s, whenAnyScope)))).Concat().Take(1)
                .TapAsync(() => whenAnyScope.RemoveScopeAwaiters());
        }
    }
}
