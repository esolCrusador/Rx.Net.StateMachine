using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public class WorkflowRegistrations
    {
        private Dictionary<string, Type>? _workflowByIds;
        private readonly IServiceProvider _serviceProvider;

        public WorkflowRegistrations(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Dictionary<string, Type> GetWorkflowByIds()
        {
            return _workflowByIds ?? GetWorkflowByIdsAsync().Result;
        }

        private async Task<Dictionary<string, Type>> GetWorkflowByIdsAsync()
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            return GetWorkflowByIds(scope.ServiceProvider);
        }

        public Dictionary<string, Type> GetWorkflowByIds(IServiceProvider serviceProvider)
        {
            if (_workflowByIds == null)
                lock (this)
                    if (_workflowByIds == null)
                        _workflowByIds = serviceProvider.GetServices<IWorkflow>().ToDictionary(wf => wf.WorkflowId, wf => wf.GetType());

            return _workflowByIds;
        }
    }
}
