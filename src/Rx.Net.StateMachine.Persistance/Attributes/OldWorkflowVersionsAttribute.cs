using System;

namespace Rx.Net.StateMachine.Persistance.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class OldWorkflowVersionsAttribute : Attribute
    {
        public OldWorkflowVersionsAttribute(params Type[] oldWorkflowVersions) => OldWorkflowVersions = oldWorkflowVersions;

        public Type[] OldWorkflowVersions { get; }
    }
}
