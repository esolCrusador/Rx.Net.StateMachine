using Microsoft.Extensions.DependencyInjection;
using Rx.Net.StateMachine.Exceptions;
using Rx.Net.StateMachine.Persistance.Attributes;
using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Persistance
{
    public static class StateMachineBootstrapper
    {
        public static IServiceCollection AddStateMachine<TContext>(this IServiceCollection services, JsonSerializerOptions? jsonSerializerOptions = null)
            where TContext : class
        {
            if (jsonSerializerOptions != null)
                services.AddSingleton(jsonSerializerOptions);
            services.AddSingleton<StateMachine>();
            services.AddSingleton<IWorkflowResolver, WorkflowResolver>();
            services.AddSingleton<WorkflowRegistrations>();
            services.AddSingleton<WorkflowManager<TContext>>();
            services.AddSingleton(sp => new WorkflowManagerAccessor<TContext>(() => sp.GetRequiredService<WorkflowManager<TContext>>()));
            services.AddSingleton<WorkflowFatalExceptions>();
            services.UseWorkflowSessionValue<TContext>();

            return services;
        }

        public static IServiceCollection UseWorkflowSessionValue<TValue>(this IServiceCollection services)
            where TValue: class
        {
            services.AddScoped<WorkflowSessionValueProvider<TValue>>();
            services.AddScoped<TValue>(sp => sp.GetRequiredService<WorkflowSessionValueProvider<TValue>>().Value);

            return services;
        }

        public static IServiceCollection WithWorkflowFatal<TException>(this IServiceCollection services)
            where TException : Exception
        {
            services.AddSingleton(new WorkflowFatalExceptionRegistration(typeof(TException), null));

            return services;
        }

        public static IServiceCollection WithWorkflowFatal<TException>(this IServiceCollection services, Func<TException, bool> filter)
            where TException : Exception
        {
            services.AddSingleton(new WorkflowFatalExceptionRegistration(typeof(TException), ex => filter((TException)ex)));

            return services;
        }

        public static IServiceCollection AddWorkflow<TWorkflow>(this IServiceCollection services)
            where TWorkflow : class, IWorkflow
        {
            services.AddScoped<TWorkflow>();
            services.AddScoped<IWorkflow>(sp => sp.GetRequiredService<TWorkflow>());

            var oldVersionsAttribute = typeof(TWorkflow).GetCustomAttribute<OldWorkflowVersionsAttribute>();
            if (oldVersionsAttribute != null)
            {
                foreach (var oldWorkflow in oldVersionsAttribute.OldWorkflowVersions)
                {
                    services.AddScoped(oldWorkflow);
                    services.AddScoped(typeof(IWorkflow), sp => sp.GetRequiredService(oldWorkflow));
                }
            }

            return services;
        }

        public static IServiceCollection BeforePersist(this IServiceCollection services, Func<IServiceProvider, SessionState, Task> beforePersist)
        {
            services.AddScoped<BeforePersist>(sp => s => beforePersist(sp, s));
            return services;
        }
    }
}
