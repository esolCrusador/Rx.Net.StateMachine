using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public interface IWorkflowFactory
    {
        string WorkflowId { get; }
        IObservable<Unit> Execute(StateMachineScope scope);
    }

    public interface IWorkflowFactory<TResult>: IWorkflowFactory
    {
        IObservable<TResult> GetResult(StateMachineScope scope);
    }

    public interface IWorkflowFactory<TSource, TResult>: IWorkflowFactory<TResult>
    {
        IObservable<TResult> GetResult(IObservable<TSource> input, StateMachineScope scope);
    }
}
