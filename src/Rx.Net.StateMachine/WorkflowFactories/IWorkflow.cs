using Rx.Net.StateMachine.Flow;
using System;
using System.Reactive;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public interface IWorkflow
    {
        string WorkflowId { get; }
        IFlow<Unit> Execute(IFlow<Unit> flow);
    }

    public interface IWorkflow<TSource>: IWorkflow
    {
        IFlow<Unit> Execute(IFlow<TSource> flow);
    }
}
