using Rx.Net.StateMachine.Flow;
using System.Reactive;
using System.Reactive.Linq;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public abstract class Workflow : IWorkflow
    {
        public abstract string WorkflowId { get; }
        public virtual bool IsPersistant => true;

        public abstract IFlow<string?> Execute(IFlow<Unit> flow);
    }

    public abstract class Workflow<TSource> : Workflow, IWorkflow<TSource>
    {
        public override IFlow<string?> Execute(IFlow<Unit> flow) =>
            Execute(new StateMachineFlow<TSource>(flow.Scope, Observable.Empty<TSource>()));

        public abstract IFlow<string?> Execute(IFlow<TSource> flow);
    }
}
