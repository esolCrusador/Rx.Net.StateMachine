using System;
using System.Reactive;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public interface IWorkflow
    {
        string WorkflowId { get; }
        IObservable<Unit> Execute(StateMachineScope scope);
    }

    public interface IWorkflow<TResult>: IWorkflow
    {
        IObservable<TResult> GetResult(StateMachineScope scope);
    }

    public interface IWorkflow<TSource, TResult>: IWorkflow<TResult>
    {
        IObservable<TResult> GetResult(IObservable<TSource> input, StateMachineScope scope);
    }
}
