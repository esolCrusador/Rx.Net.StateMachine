using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public interface IWorkflowResolver
    {
        Task<IWorkflow> GetWorkflow(string workflowId);
        Task<IWorkflow<TResult>> GetWorkflow<TResult>(string workflowId);
        Task<IWorkflow<TSource, TResult>> GetWorkflow<TSource, TResult>(string workflowId);
    }
}
