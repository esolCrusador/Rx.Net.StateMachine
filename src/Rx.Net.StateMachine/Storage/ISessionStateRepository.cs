using Rx.Net.StateMachine.States;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Storage
{
    public interface ISessionStateStorage
    {
        Task PersistStepState(SessionState sessionState);
        Task PersistEventState(SessionState sessionState);
        Task PersistEventAwaiter(SessionState sessionState);
        Task PersistSessionState(SessionState sessionState);
    }
}
