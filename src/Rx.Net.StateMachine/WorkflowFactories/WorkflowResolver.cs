using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public class WorkflowResolver : IWorkflowResolver
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly WorkflowRegistrations _workflowRegistrations;

        public WorkflowResolver(IServiceProvider serviceProvider, WorkflowRegistrations workflowRegistrations)
        {
            _serviceProvider = serviceProvider;
            _workflowRegistrations = workflowRegistrations;
        }

        public WorkflowSession GetWorkflowSession(string workflowId, BeforePersistScope? executeBeforePersist, object userContext)
        {
            var scope = _serviceProvider.CreateAsyncScope();
            scope.ServiceProvider.ProvideValue(userContext);

            var workflow = (IWorkflow)scope.ServiceProvider.GetRequiredService(
               _workflowRegistrations.GetWorkflow(workflowId)
            );
            BeforePersist beforePersist = async session =>
            {
                if (executeBeforePersist != null)
                    await executeBeforePersist(scope.ServiceProvider, session);

                await Task.WhenAll(scope.ServiceProvider.GetServices<BeforePersist>().Select(bp => bp(session)));
            };

            return new WorkflowSession(scope, workflow, beforePersist);
        }

        public WorkflowSession GetWorkflowSession<TWorkflow>(BeforePersistScope? executeBeforePersist, object userContext)
            where TWorkflow : class, IWorkflow
        {
            var scope = _serviceProvider.CreateAsyncScope();
            scope.ServiceProvider.ProvideValue(userContext);

            var wokrflow = scope.ServiceProvider.GetRequiredService<TWorkflow>();
            BeforePersist beforePersist = async session =>
            {
                if (executeBeforePersist != null)
                    await executeBeforePersist(scope.ServiceProvider, session);

                await Task.WhenAll(scope.ServiceProvider.GetServices<BeforePersist>().Select(bp => bp(session)));
            };

            return new WorkflowSession(scope, wokrflow, beforePersist);
        }
    }
}
