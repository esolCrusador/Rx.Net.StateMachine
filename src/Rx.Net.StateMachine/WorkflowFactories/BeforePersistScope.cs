using Rx.Net.StateMachine.States;
using System;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public delegate Task BeforePersistScope(IServiceProvider serviceProvider, SessionState sessionState);
}
