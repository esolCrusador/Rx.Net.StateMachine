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

        public Task<WorkflowSession> GetWorkflowSession(string workflowId, object context, BeforePersistScope? executeBeforePersist)
        {
            var scope = _serviceProvider.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ContextProvider>().SetContext(context);
            var workflow = (IWorkflow)scope.ServiceProvider.GetRequiredService(
               _workflowRegistrations.GetWorkflowByIds(scope.ServiceProvider)[workflowId]
            );
            BeforePersist beforePersist = async session =>
            {
                if (executeBeforePersist != null)
                    await executeBeforePersist(scope.ServiceProvider, session);

                await Task.WhenAll(scope.ServiceProvider.GetServices<BeforePersist>().Select(bp => bp(session)));
            };

            return Task.FromResult(new WorkflowSession(scope, workflow, beforePersist));
        }

        public Task<WorkflowSession> GetWorkflowSession<TWorkflow>(object context, BeforePersistScope? executeBeforePersist)
            where TWorkflow : class, IWorkflow
        {
            var scope = _serviceProvider.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ContextProvider>().SetContext(context);
            var wokrflow = scope.ServiceProvider.GetRequiredService<TWorkflow>();
            BeforePersist beforePersist = async session =>
            {
                if (executeBeforePersist != null)
                    await executeBeforePersist(scope.ServiceProvider, session);

                await Task.WhenAll(scope.ServiceProvider.GetServices<BeforePersist>().Select(bp => bp(session)));
            };

            return Task.FromResult(new WorkflowSession(scope, wokrflow, beforePersist));
        }
    }
}
