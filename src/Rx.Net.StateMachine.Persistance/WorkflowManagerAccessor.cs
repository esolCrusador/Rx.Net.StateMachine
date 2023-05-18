using System;

namespace Rx.Net.StateMachine.Persistance
{
    public class WorkflowManagerAccessor<TContext>
    {
        private Lazy<WorkflowManager<TContext>> _workflowManager;
        public WorkflowManager<TContext> WorkflowManager => _workflowManager.Value;

        public WorkflowManagerAccessor(Func<WorkflowManager<TContext>> createWorkflowManager)
        {
            _workflowManager = new Lazy<WorkflowManager<TContext>>(createWorkflowManager);
        }
    }
}
