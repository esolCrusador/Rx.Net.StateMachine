using Rx.Net.StateMachine.Flow;
using System.Reactive;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public interface IWorkflow
    {
        string WorkflowId { get; }
        bool IsPersistant { get; }
        IFlow<string?> Execute(IFlow<Unit> flow);
    }

    public interface IWorkflow<TSource> : IWorkflow
    {
        IFlow<string?> Execute(IFlow<TSource> flow);
    }
}
