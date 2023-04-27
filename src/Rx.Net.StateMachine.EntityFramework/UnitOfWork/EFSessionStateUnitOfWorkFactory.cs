using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.EntityFramework.Tests.UnitOfWork;
using Rx.Net.StateMachine.Persistance;

public class EFSessionStateUnitOfWorkFactory<TContext, TContextKey, TUnitOfWork> : ISessionStateUnitOfWorkFactory
    where TContext: class
    where TUnitOfWork: EFSessionStateUnitOfWork<TContext, TContextKey>, new()
{
    private readonly SessionStateDbContextFactory<TContext, TContextKey> _contextFactory;

    public EFSessionStateUnitOfWorkFactory(SessionStateDbContextFactory<TContext, TContextKey> contextFactory)
    {
        _contextFactory = contextFactory;
    }
    public ISessionStateUnitOfWork Create()
    {
        var uof = new TUnitOfWork
        {
            SessionStateDbContext = _contextFactory.CreateBase()
        };

        return uof;
    }
}