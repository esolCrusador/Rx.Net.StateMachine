using Microsoft.Extensions.DependencyInjection;
using Rx.Net.StateMachine.EntityFramework.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Rx.Net.StateMachine.Tests.Controls
{
    public static class ControlsBootstrapper
    {
        public static IServiceCollection AddControls(this IServiceCollection services, params Type[] assemblyTypes)
        {
            return AddControls(services, assemblyTypes.Select(t => t.Assembly));
        }

        public static IServiceCollection AddControls(this IServiceCollection services, IEnumerable<Assembly> controlAssemblies)
        {
            var assemblies = controlAssemblies.Concat(typeof(IControl).Assembly).Distinct();
            foreach (var assembly in assemblies)
                foreach (var controlType in assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && t.GetInterface(nameof(IControl)) != null)
                    )
                    services.AddSingleton(controlType);

            return services;
        }
    }
}
