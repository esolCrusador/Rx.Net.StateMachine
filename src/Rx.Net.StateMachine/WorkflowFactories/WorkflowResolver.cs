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

        public Task<IWorkflow> GetWorkflow(string workflowId)
        {
            return Task.FromResult(_workflowFactories[workflowId]);
        }

        public async Task<IWorkflow<TResult>> GetWorkflow<TResult>(string workflowId)
        {
            return (IWorkflow<TResult>)await GetWorkflow(workflowId);
        }

        public async Task<IWorkflow<TSource, TResult>> GetWorkflow<TSource, TResult>(string workflowId)
        {
            return (IWorkflow<TSource, TResult>)await GetWorkflow(workflowId);
        }
    }
}
