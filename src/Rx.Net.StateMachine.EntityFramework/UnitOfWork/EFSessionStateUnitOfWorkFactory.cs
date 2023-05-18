using Rx.Net.StateMachine.EntityFramework.Awaiters;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.EntityFramework.Tests.UnitOfWork;
using Rx.Net.StateMachine.EntityFramework.UnitOfWork;
using Rx.Net.StateMachine.Persistance;

public class EFSessionStateUnitOfWorkFactory<TContext, TContextKey, TUnitOfWork> : ISessionStateUnitOfWorkFactory
    where TContext: class
    where TUnitOfWork: EFSessionStateUnitOfWork<TContext, TContextKey>, new()
{
    private readonly SessionStateDbContextFactory<TContext, TContextKey> _contextFactory;
    private readonly ContextKeySelector<TContext, TContextKey> _contextKeySelector;
    private readonly AwaitHandlerResolver<TContext, TContextKey> _awaitHandlerResolver;

    public EFSessionStateUnitOfWorkFactory(SessionStateDbContextFactory<TContext, TContextKey> contextFactory, ContextKeySelector<TContext, TContextKey> contextKeySelector, AwaitHandlerResolver<TContext, TContextKey> awaitHandlerResolver)
    {
        _contextFactory = contextFactory;
        _contextKeySelector = contextKeySelector;
        _awaitHandlerResolver = awaitHandlerResolver;
    }
    public ISessionStateUnitOfWork Create()
    {
        var uof = new TUnitOfWork
        {
            SessionStateDbContext = _contextFactory.CreateBase(),
            ContextKeySelector = _contextKeySelector,
            EventAwaiterResolver = _awaitHandlerResolver
        };

        return uof;
    }
}