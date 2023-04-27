using System;

namespace Rx.Net.StateMachine.EntityFramework.ContextDfinition
{
    public class SessionStateDbContextFactory<TDbContext, TContext, TContextKey> : SessionStateDbContextFactory<TContext, TContextKey>
        where TDbContext : SessionStateDbContext<TContext, TContextKey>
        where TContext : class
    {
        private readonly Func<TDbContext> _create;

        public SessionStateDbContextFactory(Func<TDbContext> create)
        {
            _create = create;
        }
        public TDbContext Create() => _create();
        public override SessionStateDbContext<TContext, TContextKey> CreateBase() => Create();
    }

    public abstract class SessionStateDbContextFactory<TContext, TContextKey>
        where TContext : class
    {
        public abstract SessionStateDbContext<TContext, TContextKey> CreateBase();
    }
}
