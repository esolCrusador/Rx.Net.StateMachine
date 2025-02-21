using Microsoft.EntityFrameworkCore;
using System;

namespace Rx.Net.StateMachine.EntityFramework.ContextDfinition
{
    public class SessionStateDbContextFactory<TDbContext> : SessionStateDbContextFactory
        where TDbContext : DbContext
    {
        private readonly Func<TDbContext> _create;
        public SessionStateDbContextFactory(Func<TDbContext> create) => _create = create;
        public TDbContext Create() => _create();

        public override DbContext CreateBase() => Create();
    }

    public abstract class SessionStateDbContextFactory
    {
        public abstract DbContext CreateBase();
    }
}
