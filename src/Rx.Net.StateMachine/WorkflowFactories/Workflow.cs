using System;
using System.Reactive;
using System.Reactive.Linq;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public abstract class Workflow : IWorkflow
    {
        public abstract string WorkflowId { get; }

        public abstract IObservable<Unit> Execute(StateMachineScope scope);
    }

    public abstract class Workflow<TResult> : Workflow, IWorkflow<TResult>
    {
        public override IObservable<Unit> Execute(StateMachineScope scope) =>
            GetResult(scope).Select(_ => Unit.Default);

        public abstract IObservable<TResult> GetResult(StateMachineScope scope);
    }

    public abstract class Workflow<TSource, TResult> : Workflow<TResult>, IWorkflow<TSource, TResult>
    {
        public override IObservable<TResult> GetResult(StateMachineScope scope) =>
            GetResult(Observable.Empty<TSource>(), scope);

        public abstract IObservable<TResult> GetResult(IObservable<TSource> input, StateMachineScope scope);
    }
}
