using Rx.Net.StateMachine.EntityFramework.Tests.ContextDfinition;
using Rx.Net.StateMachine.EntityFramework.Tests.UnitOfWork;
using Rx.Net.StateMachine.Persistance;

public class EFSessionStateUnitOfWorkFactory<TContext, TContextKey, TUnitOfWork> : ISessionStateUnitOfWorkFactory
    where TContext: class
    where TUnitOfWork: EFSessionStateUnitOfWork<TContext, TContextKey>, new()
{
    private readonly Func<SessionStateDbContext<TContext, TContextKey>> _createContext;

    public EFSessionStateUnitOfWorkFactory(Func<SessionStateDbContext<TContext, TContextKey>> createContext)
    {
        _createContext = createContext;
    }
    public ISessionStateUnitOfWork Create()
    {
        var uof = new TUnitOfWork();
        uof.SessionStateDbContext = _createContext();

        return uof;
    }
}