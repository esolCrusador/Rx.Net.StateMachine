using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public interface IWorkflowResolver
    {
        Task<IWorkflowFactory> GetWorkflowFactory(string workflowId);
        Task<IWorkflowFactory<TResult>> GetWorkflowFactory<TResult>(string workflowId);
        Task<IWorkflowFactory<TSource, TResult>> GetWorkflowFactory<TSource, TResult>(string workflowId);
    }
}
