using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public class WorkflowResolver : IWorkflowResolver
    {
        private Dictionary<string, Type>? _workflowByIds;
        private readonly IServiceProvider _serviceProvider;

        public WorkflowResolver(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task<WorkflowSession> GetWorkflowSession(string workflowId)
        {
            var scope = _serviceProvider.CreateAsyncScope();
            var workflow = (IWorkflow)scope.ServiceProvider.GetRequiredService(GetWorkflowByIds(scope.ServiceProvider)[workflowId]);
            BeforePersist beforePersist = session => Task.WhenAll(scope.ServiceProvider.GetServices<BeforePersist>().Select(bp => bp(session)));

            return Task.FromResult(new WorkflowSession(scope, workflow, beforePersist));
        }

        public Task<WorkflowSession> GetWorkflowSession<TWorkflow>()
            where TWorkflow : class, IWorkflow
        {
            var scope = _serviceProvider.CreateAsyncScope();
            var wokrflow = scope.ServiceProvider.GetRequiredService<TWorkflow>();
            BeforePersist beforePersist = session => Task.WhenAll(scope.ServiceProvider.GetServices<BeforePersist>().Select(bp => bp(session)));

            return Task.FromResult(new WorkflowSession(scope, wokrflow, beforePersist));
        }

        private Dictionary<string, Type> GetWorkflowByIds(IServiceProvider serviceProvider)
        {
            if (_workflowByIds == null)
                lock (this)
                    if (_workflowByIds == null)
                        _workflowByIds = serviceProvider.GetServices<IWorkflow>().ToDictionary(wf => wf.WorkflowId, wf => wf.GetType());

            return _workflowByIds;
        }
    }
}
