using Microsoft.EntityFrameworkCore;
using Polly;
using Rx.Net.StateMachine.EntityFramework.Tables;
using Rx.Net.StateMachine.EntityFramework.Tests.Tables;

namespace Rx.Net.StateMachine.EntityFramework.ContextDfinition
{
    public static class SessionStateDbContextExtensions
    {
        public static void ConfigureSessions<TContext, TContextKey>(this ModelBuilder modelBuilder)
            where TContext : class
        {
            modelBuilder.Entity<SessionStateTable<TContext, TContextKey>>();
            modelBuilder.Entity<SessionEventAwaiterTable<TContext, TContextKey>>(builder =>
            {
                builder.HasIndex(aw => new { aw.IsActive, aw.Identifier }).HasFilter("[IsActive] = 1");
            });
            modelBuilder.Entity<TContext>();
        }
    }
}
