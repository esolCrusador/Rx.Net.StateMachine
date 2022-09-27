using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public abstract class WorkflowFactory : IWorkflowFactory
    {
        public abstract string WorkflowId { get; }

        public abstract IObservable<Unit> Execute(StateMachineScope scope);
    }

    public abstract class WorkflowFactory<TResult> : WorkflowFactory, IWorkflowFactory<TResult>
    {
        public override IObservable<Unit> Execute(StateMachineScope scope) =>
            GetResult(scope).Select(_ => Unit.Default);

        public abstract IObservable<TResult> GetResult(StateMachineScope scope);
    }

    public abstract class WorkflowFactory<TSource, TResult> : WorkflowFactory<TResult>, IWorkflowFactory<TSource, TResult>
    {
        public override IObservable<TResult> GetResult(StateMachineScope scope) =>
            GetResult(Observable.Empty<TSource>(), scope);

        public abstract IObservable<TResult> GetResult(IObservable<TSource> input, StateMachineScope scope);
    }
}
