using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public class WorkflowResolver : IWorkflowResolver
    {
        private readonly Dictionary<string, IWorkflow> _workflowFactories;

        public WorkflowResolver(IEnumerable<IWorkflow> workflowFactories)
        {
            _workflowFactories = workflowFactories.ToDictionary(wf => wf.WorkflowId);
        }

        public Task<IWorkflow> GetWorkflowFactory(string workflowId)
        {
            return Task.FromResult(_workflowFactories[workflowId]);
        }

        public async Task<IWorkflow<TResult>> GetWorkflowFactory<TResult>(string workflowId)
        {
            return (IWorkflow<TResult>)await GetWorkflowFactory(workflowId);
        }

        public async Task<IWorkflow<TSource, TResult>> GetWorkflowFactory<TSource, TResult>(string workflowId)
        {
            return (IWorkflow<TSource, TResult>)await GetWorkflowFactory(workflowId);
        }
    }
}
