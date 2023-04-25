using Rx.Net.StateMachine.Persistance.Entities;

namespace Rx.Net.StateMachine.Persistance
{
    public interface ISessionStateUnitOfWorkFactory
    {
        ISessionStateUnitOfWork Create();
    }
}
