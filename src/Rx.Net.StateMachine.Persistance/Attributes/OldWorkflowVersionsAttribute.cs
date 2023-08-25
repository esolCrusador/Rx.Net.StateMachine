using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Persistance.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class OldWorkflowVersionsAttribute : Attribute
    {
        public OldWorkflowVersionsAttribute(params Type[] oldWorkflowVersions)
        {
            OldWorkflowVersions = oldWorkflowVersions;
        }

        public Type[] OldWorkflowVersions { get; }
    }
}
