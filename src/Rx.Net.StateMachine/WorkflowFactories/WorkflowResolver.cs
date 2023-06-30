using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public class WorkflowResolver : IWorkflowResolver
    {
        private readonly Dictionary<string, IWorkflow> _workflowByIds;
        private readonly Dictionary<Type, IWorkflow> _workflowByTypes;

        public WorkflowResolver(IEnumerable<IWorkflow> workflowFactories)
        {
            _workflowByIds = workflowFactories.ToDictionary(wf => wf.WorkflowId);
            _workflowByTypes = workflowFactories.ToDictionary(wf => wf.GetType());
        }

        public Task<IWorkflow> GetWorkflow(string workflowId)
        {
            return Task.FromResult(_workflowByIds[workflowId]);
        }

        public async Task<IWorkflow<TSource>> GetWorkflow<TSource>(string workflowId)
        {
            return (IWorkflow<TSource>)await GetWorkflow(workflowId);
        }

        public Task<TWorkflow> GetWorkflow<TWorkflow>() where TWorkflow : IWorkflow
        {
            return Task.FromResult((TWorkflow)_workflowByTypes[typeof(TWorkflow)]);
        }
    }
}
