using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public interface IWorkflowResolver
    {
        Task<IWorkflow> GetWorkflowFactory(string workflowId);
        Task<IWorkflow<TResult>> GetWorkflowFactory<TResult>(string workflowId);
        Task<IWorkflow<TSource, TResult>> GetWorkflowFactory<TSource, TResult>(string workflowId);
    }
}
