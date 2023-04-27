using Microsoft.Extensions.DependencyInjection;
using Rx.Net.StateMachine.WorkflowFactories;
using System.Text.Json;

namespace Rx.Net.StateMachine.Persistance
{
    public static class StateMachineBootstrapper
    {
        public static IServiceCollection AddStateMachine<TContext>(this IServiceCollection services, JsonSerializerOptions? jsonSerializerOptions = null)
        {
            if (jsonSerializerOptions != null)
                services.AddSingleton(jsonSerializerOptions);
            services.AddSingleton<StateMachine>();
            services.AddSingleton<IWorkflowResolver, WorkflowResolver>();
            services.AddSingleton<WorkflowManager<TContext>>();
            services.AddSingleton(sp => new WorkflowManagerAccessor<TContext>(() => sp.GetRequiredService<WorkflowManager<TContext>>()));

            return services;
        }

        public static IServiceCollection AddWorkflowFactory<TWorkflowFactory>(this IServiceCollection services)
            where TWorkflowFactory: class, IWorkflowFactory
        {
            services.AddSingleton<IWorkflowFactory, TWorkflowFactory>();
            return services;
        }
    }
}
