using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.ObservableExtensions
{
    public static class TaskObservableExtensions
    {
        public static IObservable<IObservable<TResult>> SelectAsync<TSource, TResult>(this IObservable<TSource> source, Func<TSource, CancellationToken, Task<TResult>> execute) =>
            source.Select(source =>
            {
                return Observable.FromAsync(cancellation => execute(source, cancellation));
            });

        public static IObservable<IObservable<TResult>> SelectAsync<TSource, TResult>(this IObservable<TSource> source, Func<TSource, Task<TResult>> execute) =>
            source.Select(source =>
            {
                return Observable.FromAsync(cancellation => execute(source));
            });

        public static IObservable<IObservable<Unit>> SelectAsync<TSource>(this IObservable<TSource> source, Func<TSource, CancellationToken, Task> execute) =>
            source.Select(source =>
            {
                return Observable.FromAsync(cancellation => execute(source, cancellation));
            });

        public static IObservable<IObservable<Unit>> SelectAsync<TSource>(this IObservable<TSource> source, Func<TSource, Task> execute) =>
            source.Select(source =>
            {
                return Observable.FromAsync(cancellation => execute(source));
            });

        public static IObservable<TSource> TapAsync<TSource>(this IObservable<TSource> source, Func<TSource, Task> execute)
        {
            return source.SelectAsync(async source =>
            {
                await execute(source);
                return source;
            }).Concat();
        }
    }
}
