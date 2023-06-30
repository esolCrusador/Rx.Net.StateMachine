using System;
using System.Reactive;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public interface IWorkflow
    {
        string WorkflowId { get; }
        IObservable<Unit> Execute(StateMachineScope scope);
    }

    public interface IWorkflow<TSource>: IWorkflow
    {
        IObservable<Unit> Execute(IObservable<TSource> input, StateMachineScope scope);
    }
}
