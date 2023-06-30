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

    public abstract class Workflow<TSource> : Workflow, IWorkflow<TSource>
    {
        public override IObservable<Unit> Execute(StateMachineScope scope) =>
            Execute(Observable.Empty<TSource>(), scope);

        public abstract IObservable<Unit> Execute(IObservable<TSource> input, StateMachineScope scope);
    }
}
