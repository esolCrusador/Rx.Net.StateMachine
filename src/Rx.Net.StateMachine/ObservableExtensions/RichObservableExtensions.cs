﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            EnsureNotNestedObservable<TSource>();

            return source.Select(s => Unit.Default);
        }

        public static IObservable<TResult> MapTo<TSource, TResult>(this IObservable<TSource> source, TResult result)
        {
            EnsureNotNestedObservable<TSource>();

            return source.Select(s => result);
        }

        public static IObservable<TResult> WhenAny<TSource, TResult>(this IObservable<TSource> source, StateMachineScope scope, string name, params Func<TSource, StateMachineScope, IObservable<TResult>?>[] observables)
        {
            var whenAnyScope = scope.BeginScope(name);
            return source.Select(s =>
            {
                var results = observables.Select(obs => obs(s, whenAnyScope)).Where(obs => obs != null) as IEnumerable<IObservable<TResult>>;
                return Observable.Merge(results);
            }).Concat().Take(1)
                .TapAsync(() => whenAnyScope.RemoveScopeAwaiters());
        }

        private static void EnsureNotNestedObservable<TSource>()
        {
#if DEBUG
            if (typeof(TSource).IsAssignableFrom(typeof(IObservable<>)))
                throw new InvalidOperationException($"Maping to void Observable of Observables");
# endif
        }
    }
}
