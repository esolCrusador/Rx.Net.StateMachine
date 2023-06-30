using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public interface IWorkflowResolver
    {
        Task<IWorkflow> GetWorkflow(string workflowId);
        Task<IWorkflow<TSource>> GetWorkflow<TSource>(string workflowId);
        Task<TWorkflow> GetWorkflow<TWorkflow>() where TWorkflow : IWorkflow;
    }
}
