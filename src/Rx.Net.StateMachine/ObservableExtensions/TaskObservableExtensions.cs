using System;
using System.Collections.Generic;
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
    }
}
