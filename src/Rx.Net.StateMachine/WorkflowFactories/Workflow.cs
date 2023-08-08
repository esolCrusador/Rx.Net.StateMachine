using Rx.Net.StateMachine.Flow;
using System;
using System.Reactive;
using System.Reactive.Linq;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public abstract class Workflow : IWorkflow
    {
        public abstract string WorkflowId { get; }

        public abstract IFlow<Unit> Execute(IFlow<Unit> flow);
    }

    public abstract class Workflow<TSource> : Workflow, IWorkflow<TSource>
    {
        public override IFlow<Unit> Execute(IFlow<Unit> flow) =>
            Execute(new StateMachineFlow<TSource>(flow.Scope, Observable.Empty<TSource>()));

        public abstract IFlow<Unit> Execute(IFlow<TSource> flow);
    }
}
