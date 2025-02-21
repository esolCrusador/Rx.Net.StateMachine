namespace Rx.Net.StateMachine.WorkflowFactories
{
    public interface IWorkflowResolver
    {
        WorkflowSession GetWorkflowSession(string workflowId, BeforePersistScope? executeBeforePersist, object userContext);
        WorkflowSession GetWorkflowSession<TWorkflow>(BeforePersistScope? executeBeforePersist, object userContext) where TWorkflow : class, IWorkflow;
    }
}
