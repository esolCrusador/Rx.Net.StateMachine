using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public interface IWorkflowResolver
    {
        Task<WorkflowSession> GetWorkflowSession(string workflowId);
        Task<WorkflowSession> GetWorkflowSession<TWorkflow>() where TWorkflow : class, IWorkflow;
    }
}
