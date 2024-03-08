using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public interface IWorkflowResolver
    {
        Task<WorkflowSession> GetWorkflowSession(string workflowId, BeforePersistScope? executeBeforePersist);
        Task<WorkflowSession> GetWorkflowSession<TWorkflow>(BeforePersistScope? executeBeforePersist) where TWorkflow : class, IWorkflow;
    }
}
