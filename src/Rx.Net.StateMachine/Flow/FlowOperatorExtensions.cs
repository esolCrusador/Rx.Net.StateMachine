using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Flow
{
    public static class FlowOperatorExtensions
    {
        public static IFlow<TResult> Select<TSource, TResult>(this IFlow<TSource> source, Func<TSource, TResult> execute)
        {
            return new StateMachineFlow<TResult>(source.Scope, source.Observable.Select(s => execute(s)));
        }

        public static IFlow<Unit> Select<TSource>(this IFlow<TSource> source, Action<TSource> execute)
        {
            return new StateMachineFlow<Unit>(source.Scope, source.Observable.Select(s =>
            {
                execute(s);
                return Unit.Default;
            }));
        }

        public static IFlow<Unit> Select<TSource>(this IFlow<TSource> source, Action<TSource, StateMachineScope> execute)
        {
            return new StateMachineFlow<Unit>(source.Scope, source.Observable.Select(s =>
            {
                execute(s, source.Scope);
                return Unit.Default;
            }));
        }

        public static IFlow<TResult> Select<TSource, TResult>(this IFlow<TSource> source, Func<TSource, IFlow<TResult>> execute)
        {
            return new StateMachineFlow<TResult>(source.Scope, source.Observable.Select(s => execute(s).Observable).Concat());
        }

        public static IFlow<TResult> Select<TSource, TResult>(this IFlow<TSource> source, Func<TSource, StateMachineScope, TResult> execute)
        {
            return new StateMachineFlow<TResult>(source.Scope, source.Observable.Select(s => execute(s, source.Scope)));
        }

        public static IFlow<TResult> Select<TSource, TResult>(this IFlow<TSource> source, Func<TSource, StateMachineScope, IFlow<TResult>> execute)
        {
            return new StateMachineFlow<TResult>(source.Scope, source.Observable.Select(s => execute(s, source.Scope).Observable).Concat());
        }

        public static IFlow<Unit> SelectAsync<TSource>(this IFlow<TSource> source, Func<TSource, Task> execute)
        {
            return new StateMachineFlow<Unit>(source.Scope, source.Observable.Select(async s =>
            {
                await execute(s);
                return Unit.Default;
            }).Concat());
        }

        public static IFlow<Unit> SelectAsync<TSource>(this IFlow<TSource> source, Func<Task> execute)
        {
            return new StateMachineFlow<Unit>(source.Scope, source.Observable.Select(async s =>
            {
                await execute();
                return Unit.Default;
            }).Concat());
        }

        public static IFlow<TResult> SelectAsync<TSource, TResult>(this IFlow<TSource> source, Func<TSource, Task<TResult>> execute)
        {
            return new StateMachineFlow<TResult>(source.Scope, source.Observable.Select(s => execute(s)).Concat());
        }

        public static IFlow<TResult> SelectAsync<TSource, TResult>(this IFlow<TSource> source, Func<TSource, Task<IFlow<TResult>>> execute)
        {
            return new StateMachineFlow<TResult>(source.Scope, source.Observable.Select(s => execute(s)).Concat().Select(fw => fw.Observable).Concat());
        }

        public static IFlow<TResult> SelectAsync<TSource, TResult>(this IFlow<TSource> source, Func<TSource, StateMachineScope, Task<TResult>> execute)
        {
            return new StateMachineFlow<TResult>(source.Scope, source.Observable.Select(s => execute(s, source.Scope)).Concat());
        }

        public static IFlow<TResult> SelectAsync<TSource, TResult>(this IFlow<TSource> source, Func<TSource, StateMachineScope, Task<IFlow<TResult>>> execute)
        {
            return new StateMachineFlow<TResult>(source.Scope, source.Observable.Select(s => execute(s, source.Scope)).Concat().Select(fw => fw.Observable).Concat());
        }

        public static IFlow<TResult> SelectAsync<TSource, TResult>(this IFlow<TSource> source, Func<Task<IFlow<TResult>>> execute)
        {
            return new StateMachineFlow<TResult>(source.Scope, source.Observable.Select(s => execute()).Concat().Select(fw => fw.Observable).Concat());
        }

        public static IFlow<Unit> SelectAsync<TSource>(this IFlow<TSource> source, Func<TSource, StateMachineScope, Task> execute)
        {
            return new StateMachineFlow<Unit>(source.Scope, source.Observable.Select(async s =>
            {
                await execute(s, source.Scope);
                return Unit.Default;
            }).Concat());
        }

        public static IFlow<TResult> SelectAsync<TSource, TResult>(this IFlow<TSource> source, Func<Task<TResult>> execute)
        {
            return new StateMachineFlow<TResult>(source.Scope, source.Observable.Select(s => execute()).Concat());
        }

        public static IFlow<TElement> TapAsync<TElement>(this IFlow<TElement> flow, Func<TElement, Task> execute)
        {
            return new StateMachineFlow<TElement>(flow.Scope, flow.Observable.Select(async e =>
            {
                await execute(e);
                return e;
            }).Concat());
        }

        public static IFlow<TElement> TapAsync<TElement>(this IFlow<TElement> flow, Func<Task> execute)
        {
            return new StateMachineFlow<TElement>(flow.Scope, flow.Observable.Select(async e =>
            {
                await execute();
                return e;
            }).Concat());
        }

        public static IFlow<TElement> TapAsync<TElement>(this IFlow<TElement> flow, Func<TElement, StateMachineScope, Task> execute)
        {
            return new StateMachineFlow<TElement>(flow.Scope, flow.Observable.Select(async e =>
            {
                await execute(e, flow.Scope);
                return e;
            }).Concat());
        }

        public static IFlow<TElement> Tap<TElement>(this IFlow<TElement> flow, Action<TElement> execute)
        {
            return new StateMachineFlow<TElement>(flow.Scope, flow.Observable.Select(e =>
            {
                execute(e);
                return e;
            }));
        }

        public static IFlow<TElement> Tap<TElement>(this IFlow<TElement> flow, Action<TElement, StateMachineScope> execute)
        {
            return new StateMachineFlow<TElement>(flow.Scope, flow.Observable.Select(e =>
            {
                execute(e, flow.Scope);
                return e;
            }));
        }

        public delegate Task FinallyDelegate<TSource>(bool isExecuted, TSource? source, Exception? ex);

        public static IFlow<TSource> FinallyAsync<TSource>(this IFlow<TSource> flow, FinallyDelegate<TSource> handle)
        {
            return new StateMachineFlow<TSource>(flow.Scope, Observable.Create<TSource>(observer =>
            {
                bool isExecuted = false;
                bool isFinalized = false;
                TSource? lastSource = default;
                var subscrption = flow.Observable.Subscribe(
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
            }));
        }

        public static IFlow<Unit> MapToVoid<TSource>(this IFlow<TSource> source)
        {
            return source.Select(s => Unit.Default);
        }

        public static IFlow<TResult> MapTo<TSource, TResult>(this IFlow<TSource> source, TResult result)
        {
            return source.Select(s => result);
        }

        public static IFlow<TElement> TapException<TElement>(this IFlow<TElement> source, Action<Exception> tap) =>
            TapException(source, (ex, scope) => tap(ex));

        public static IFlow<TElement> TapException<TElement>(this IFlow<TElement> source, Action<Exception, StateMachineScope> tap)
        {
            IObservable<TElement> cought = source.Observable.Catch<TElement, Exception>(ex =>
            {
                tap(ex, source.Scope);
                return Observable.Throw<TElement>(ex);
            });

            return new StateMachineFlow<TElement>(source.Scope, cought);
        }
    }
}
