using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public class WorkflowSession : IAsyncDisposable
    {
        private readonly AsyncServiceScope _scope;

        public WorkflowSession(AsyncServiceScope scope, IWorkflow workflow, BeforePersist beforePersist)
        {
            _scope = scope;
            Workflow = workflow;
            BeforePersist = beforePersist;
        }
        public IWorkflow Workflow { get; }
        public BeforePersist BeforePersist { get; }
        public void Provide<TValue>(TValue value)
            where TValue : class
        {
            var vp = _scope.ServiceProvider.GetService<WorkflowSessionValueProvider<TValue>>()
                ?? throw new InvalidOperationException($"Use serviceCollection.UseWorkflowSessionValue<{typeof(TValue).Name}>()");
            vp.InitValue(value);
        }

        public ValueTask DisposeAsync() => _scope.DisposeAsync();
    }
}
