using Rx.Net.StateMachine.Persistance.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Rx.Net.StateMachine.Persistance
{
    public class WorkflowManagerAccessor<TSessionState, TContext>
        where TSessionState : SessionStateBaseEntity
    {
        private bool _isInitialized;
        public WorkflowManager<TSessionState, TContext> WorkflowManager { get; private set; }

        public void Initialize(WorkflowManager<TSessionState, TContext> workflowManager)
        {
            if (_isInitialized)
                throw new InvalidOperationException($"{nameof(WorkflowManagerAccessor<TSessionState, TContext>)} is already initialized");

            _isInitialized = true;
            WorkflowManager = workflowManager;
        }
    }
}
