using System;
using System.Collections.Generic;
using System.Text;

namespace Rx.Net.StateMachine.Tests.Persistence
{
    public class WorkflowManagerAccessor
    {
        private bool _isInitialized;
        public WorkflowManager WorkflowManager { get; private set; }

        public void Initialize(WorkflowManager workflowManager)
        {
            if (_isInitialized)
                throw new InvalidOperationException($"{nameof(WorkflowManagerAccessor)} is already initialized");

            _isInitialized = true;
            WorkflowManager = workflowManager;
        }
    }
}
