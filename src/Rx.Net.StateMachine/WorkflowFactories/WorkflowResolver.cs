using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public class WorkflowResolver : IWorkflowResolver
    {
        private readonly Dictionary<string, IWorkflowFactory> _workflowFactories;

        public WorkflowResolver(params IWorkflowFactory[] workflowFactories)
            : this((IEnumerable<IWorkflowFactory>)workflowFactories)
        {
        }
        public WorkflowResolver(IEnumerable<IWorkflowFactory> workflowFactories)
        {
            _workflowFactories = workflowFactories.ToDictionary(wf => wf.WorkflowId);
        }

        public Task<IWorkflowFactory> GetWorkflowFactory(string workflowId)
        {
            return Task.FromResult(_workflowFactories[workflowId]);
        }

        public async Task<IWorkflowFactory<TResult>> GetWorkflowFactory<TResult>(string workflowId)
        {
            return (IWorkflowFactory<TResult>)await GetWorkflowFactory(workflowId);
        }

        public async Task<IWorkflowFactory<TSource, TResult>> GetWorkflowFactory<TSource, TResult>(string workflowId)
        {
            return (IWorkflowFactory<TSource, TResult>)await GetWorkflowFactory(workflowId);
        }
    }
}
