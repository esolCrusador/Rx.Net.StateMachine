using System;
using System.Reflection;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public class WorkflowRegistration
    {
        public required string Id { get; set; }
        public required Type Type { get; set; }

        public static WorkflowRegistration Create(Type workflowType, string? workflowId = default)
        {
            return new WorkflowRegistration
            {
                Id = workflowId ?? (string?)workflowType.GetField("Id", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)!.GetRawConstantValue()
                    ?? throw new ArgumentException($"Id was not initialized"),
                Type = workflowType
            };
        }

        public static WorkflowRegistration Create<TWorkflow>(string? workflowId = default)
            where TWorkflow : IWorkflow
        {
            return Create(typeof(TWorkflow), workflowId);
        }
    }
}
