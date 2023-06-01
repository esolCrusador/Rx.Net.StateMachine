namespace Rx.Net.StateMachine.Persistance
{
    public interface ISessionStateUnitOfWorkFactory
    {
        ISessionStateUnitOfWork Create();
    }
}
