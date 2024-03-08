using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public interface IWorkflowResolver
    {
        Task<WorkflowSession> GetWorkflowSession(string workflowId, object context, BeforePersistScope? executeBeforePersist);
        Task<WorkflowSession> GetWorkflowSession<TWorkflow>(object context, BeforePersistScope? executeBeforePersist) where TWorkflow : class, IWorkflow;
    }
}
