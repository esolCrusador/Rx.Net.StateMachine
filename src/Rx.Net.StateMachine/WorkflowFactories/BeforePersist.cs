using Rx.Net.StateMachine.States;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public delegate Task BeforePersist(SessionState sessionState);
}
