using System;
using System.Collections.Generic;
using System.Linq;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public class WorkflowRegistrations
    {
        private readonly Dictionary<string, Type> _workflowByIds;
        private readonly HashSet<string> _workflowIds;

        public WorkflowRegistrations(IEnumerable<WorkflowRegistration> workflowRegistrations)
        {
            _workflowByIds = workflowRegistrations.ToDictionary(wf => wf.Id, wf => wf.Type);
            _workflowIds = _workflowByIds.Keys.ToHashSet();
        }

        public Type GetWorkflow(string workflowId) => _workflowByIds[workflowId];
        public IReadOnlyCollection<string> GetWorkflowIds() => _workflowIds;
    }
}
