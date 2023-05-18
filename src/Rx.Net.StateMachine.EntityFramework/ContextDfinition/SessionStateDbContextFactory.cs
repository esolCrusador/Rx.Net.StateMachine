using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.EntityFramework.ContextDfinition
{
    public class SessionStateDbContextFactory<TDbContext, TContext, TContextKey> : SessionStateDbContextFactory<TContext, TContextKey>
        where TDbContext : SessionStateDbContext<TContext, TContextKey>
        where TContext : class
    {
        private readonly GlobalContextState _globalContextState = new();
        private readonly Func<TDbContext> _create;
        public void ExecuteBeforeNextSaveChanges(Func<Task> execute) => _globalContextState.OnBeforeNextSaveChanges(execute);
        public SessionStateDbContextFactory(Func<TDbContext> create)
        {
            _create = create;
        }
        public TDbContext Create()
        {
            var dbContext = _create();
            dbContext.SetGlobalContextState(_globalContextState);
            return dbContext;
        }

        public override SessionStateDbContext<TContext, TContextKey> CreateBase() => Create();
    }

    public abstract class SessionStateDbContextFactory<TContext, TContextKey>
        where TContext : class
    {
        public abstract SessionStateDbContext<TContext, TContextKey> CreateBase();
    }
}
