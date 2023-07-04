using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.ObservableExtensions
{
    public static class AsyncObservableExtensions
    {
        public static IObservable<IObservable<TResult>> SelectTask<TSource, TResult>(this IObservable<TSource> source, Func<TSource, CancellationToken, Task<TResult>> execute) =>
            source.Select(source =>
            {
                return Observable.FromAsync(cancellation => execute(source, cancellation));
            });

        public static IObservable<IObservable<TResult>> SelectTask<TSource, TResult>(this IObservable<TSource> source, Func<TSource, Task<TResult>> execute) =>
            source.Select(source =>
            {
                return Observable.FromAsync(cancellation => execute(source));
            });

        public static IObservable<IObservable<TResult>> SelectTask<TSource, TResult>(this IObservable<TSource> source, Func<Task<TResult>> execute) =>
            source.Select(source =>
            {
                return Observable.FromAsync(cancellation => execute());
            });

        public static IObservable<IObservable<Unit>> SelectTask<TSource>(this IObservable<TSource> source, Func<TSource, CancellationToken, Task> execute) =>
            source.Select(source =>
            {
                return Observable.FromAsync(cancellation => execute(source, cancellation));
            });

        public static IObservable<IObservable<Unit>> SelectTask<TSource>(this IObservable<TSource> source, Func<TSource, Task> execute) =>
            source.Select(source =>
            {
                return Observable.FromAsync(cancellation => execute(source));
            });

        public static IObservable<IObservable<Unit>> SelectTask<TSource>(this IObservable<TSource> source, Func<Task> execute) =>
            source.Select(source =>
            {
                return Observable.FromAsync(cancellation => execute());
            });

        public static IObservable<TResult> SelectAsync<TSource, TResult>(this IObservable<TSource> source, Func<TSource, CancellationToken, Task<TResult>> execute) =>
            SelectTask(source, execute).Concat();

        public static IObservable<TResult> SelectAsync<TSource, TResult>(this IObservable<TSource> source, Func<TSource, Task<TResult>> execute) =>
            SelectTask(source, execute).Concat();

        public static IObservable<TResult> SelectAsync<TSource, TResult>(this IObservable<TSource> source, Func<Task<TResult>> execute) =>
            SelectTask(source, execute).Concat();

        public static IObservable<Unit> SelectAsync<TSource>(this IObservable<TSource> source, Func<TSource, CancellationToken, Task> execute) =>
            SelectTask(source, execute).Concat();

        public static IObservable<Unit> SelectAsync<TSource>(this IObservable<TSource> source, Func<TSource, Task> execute) =>
            SelectTask(source, execute).Concat();

        public static IObservable<Unit> SelectAsync<TSource>(this IObservable<TSource> source, Func<Task> execute) =>
            SelectTask(source, execute).Concat();

        public static IObservable<TSource> TapAsync<TSource>(this IObservable<TSource> source, Func<TSource, Task> execute)
        {
            return source.SelectAsync(async source =>
            {
                await execute(source);
                return source;
            });
        }

        public static IObservable<TSource> TapAsync<TSource>(this IObservable<TSource> source, Func<Task> execute)
        {
            return source.SelectAsync(async source =>
            {
                await execute();
                return source;
            });
        }

        public delegate Task FinallyDelegate<TSource>(bool isExecuted, TSource source, Exception ex);

        public static IObservable<TSource> FinallyAsync<TSource>(this IObservable<TSource> sourceObservable, FinallyDelegate<TSource> handle)
        {
            return Observable.Create<TSource>(observer =>
            {
                bool isExecuted = false;
                bool isFinalized = false;
                TSource lastSource = default;
                var subscrption = sourceObservable.Subscribe(
                    source =>
                    {
                        lastSource = source;
                        isExecuted = true;
                        observer.OnNext(source);
                    },
                    ex =>
                    {
                        handle(true, lastSource, ex).ContinueWith(_ => observer.OnError(ex));
                        isFinalized = true;
                    },
                    () =>
                    {
                        handle(isExecuted, lastSource, null).ContinueWith(_ => observer.OnCompleted());
                        isFinalized = true;
                    });

                return () =>
                {
                    if (!isFinalized)
                        handle(isExecuted, lastSource, null);
                    subscrption.Dispose();
                };
            });
        }
    }
}
