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

        public static IServiceCollection AddWorkflow<TWorkflow>(this IServiceCollection services)
            where TWorkflow : class, IWorkflow
        {
            services.AddSingleton<TWorkflow>();
            services.AddSingleton<IWorkflow>(sp => sp.GetRequiredService<TWorkflow>());
            return services;
        }
    }
}
