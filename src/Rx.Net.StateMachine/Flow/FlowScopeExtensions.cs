using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Flow
{
    public static class FlowScopeExtensions
    {
        public static IFlow<Unit> StartFlow(this StateMachineScope scope)
        {
            return new StateMachineFlow<Unit>(scope, Enumerable.Repeat(Unit.Default, 1).ToObservable());
        }

        public static IFlow<TElement> StartFlow<TElement>(this StateMachineScope scope, TElement element)
        {
            return new StateMachineFlow<TElement>(scope, Enumerable.Repeat(element, 1).ToObservable());
        }

        public static IFlow<Unit> StartFlow(this StateMachineScope scope, Func<StateMachineScope, Task> execute)
        {
            return new StateMachineFlow<Unit>(scope, Observable.FromAsync(() => execute(scope)));
        }

        public static IFlow<TElement> StartFlow<TElement>(this StateMachineScope scope, Func<StateMachineScope, Task<TElement>> execute)
        {
            return new StateMachineFlow<TElement>(scope, Observable.FromAsync(() => execute(scope)));
        }

        public static IFlow<Unit> StartFlow(this StateMachineScope scope, Func<Task> execute)
        {
            return new StateMachineFlow<Unit>(scope, Observable.FromAsync(() => execute()));
        }

        public static IFlow<TElement> StartFlow<TElement>(this StateMachineScope scope, Func<Task<TElement>> execute)
        {
            return new StateMachineFlow<TElement>(scope, Observable.FromAsync(() => execute()));
        }

        public static IFlow<TResult> Loop<TResult>(this StateMachineScope scope, string prefix, Func<StateMachineScope, IFlow<TResult>> iteration, Func<TResult, bool> exit)
        {
            return Loop(scope, prefix, iteration, (result, scope) => exit(result));
        }

        public static IFlow<TResult> Loop<TResult>(this StateMachineScope scope, string prefix, Func<StateMachineScope, IFlow<TResult>> iteration, Func<TResult, StateMachineScope, bool>? exit = null)
        {
            return scope.StartFlow().SelectAsync(async () =>
                LoopIteration(await scope.BeginRecursiveScopeAsync(prefix), iteration, exit)
            );
        }

        private static IFlow<TResult> LoopIteration<TResult>(StateMachineScope input, Func<StateMachineScope, IFlow<TResult>> iteration, Func<TResult, StateMachineScope, bool>? exit = null)
        {
            return iteration(input).Select((r, scope) =>
            {
                if (exit?.Invoke(r, scope) == true)
                    return scope.StartFlow(r);

                return scope.StartFlow(() => scope.IncreaseRecursionDepthAsync())
                    .Select(_ => LoopIteration(scope, iteration, exit));
            });
        }
    }
}
