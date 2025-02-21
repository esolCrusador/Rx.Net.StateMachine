using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public class WorkflowSessionValueProvider<TValue>
        where TValue : class
    {
        private TValue? _value;
        public TValue Value => _value ?? throw new InvalidOperationException($"Value {typeof(TValue).FullName} was not provided");

        public void InitValue(TValue value) => _value = value;
    }

    public static class WorkflowSessionValueProviderExtensions
    {
        private static readonly MethodInfo ProvideValueMethod = typeof(WorkflowSessionValueProviderExtensions)
            .GetMethods().Where(m => m.Name == nameof(ProvideValue) && m.IsGenericMethod)
            .Single();
        private static readonly ConcurrentDictionary<Type, Action<IServiceProvider, object>> _registrationDelegates = new();
        public static void ProvideValue(this IServiceProvider serviceProvider, object value)
        {
            GetRegistrationDelegate(value.GetType())(serviceProvider, value);
        }
        public static void ProvideValue<TValue>(this IServiceProvider serviceProvider, TValue value)
            where TValue : class
        {
            var vp = serviceProvider.GetService<WorkflowSessionValueProvider<TValue>>()
                ?? throw new InvalidOperationException($"Use serviceCollection.UseWorkflowSessionValue<{typeof(TValue).Name}>()");
            vp.InitValue(value);
        }

        private static Action<IServiceProvider, object> GetRegistrationDelegate(Type type) =>
            _registrationDelegates.GetOrAdd(type, CreateRegistrationDelegate);

        private static Action<IServiceProvider, object> CreateRegistrationDelegate(Type type)
        {
            var sp = Expression.Parameter(typeof(IServiceProvider), "sp");
            var value = Expression.Parameter(typeof(object), "value");

            return Expression.Lambda<Action<IServiceProvider, object>>(
                Expression.Call(ProvideValueMethod.MakeGenericMethod(type), sp, Expression.Convert(value, type)),
                sp, value
            ).Compile();
        }
    }
}
