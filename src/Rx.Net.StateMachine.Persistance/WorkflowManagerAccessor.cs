using Rx.Net.StateMachine.Persistance.Entities;
using System;

namespace Rx.Net.StateMachine.Persistance
{
    public class WorkflowManagerAccessor<TContext>
    {
        private bool _isInitialized;
        public WorkflowManager<TContext>? WorkflowManager { get; private set; }

        public void Initialize(WorkflowManager<TContext> workflowManager)
        {
            if (_isInitialized)
                throw new InvalidOperationException($"{nameof(WorkflowManagerAccessor<TContext>)} is already initialized");

            _isInitialized = true;
            WorkflowManager = workflowManager;
        }
    }
}
