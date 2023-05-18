using Rx.Net.StateMachine.States;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Storage
{
    public interface ISessionStateStorage
    {
        Task PersistStepState(SessionState sessionState);
        Task PersistItemState(SessionState sessionState);
        Task PersistEventState(SessionState sessionState);
        Task PersistEventAwaiter(SessionState sessionState);
        Task PersistIsDefault(SessionState sessionState);
        Task PersistSessionState(SessionState sessionState);
    }
}
