using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rx.Net.StateMachine.EntityFramework.Tables;
using Rx.Net.StateMachine.EntityFramework.Tests.Tables;
using System;

namespace Rx.Net.StateMachine.EntityFramework.ContextDfinition
{
    public class SessionStateTableOptions
    {
        public string SessionStateTableName { get; set; } = "SessionStates";
        public string SessionEventAwaiterTableName { get; set; } = "SessionStateAwaiters";
    }
    public static class SessionStateDbContextExtensions
    {
        public static void ConfigureSessions<TContext, TContextKey>(this ModelBuilder modelBuilder, Action<IOptions<SessionStateTableOptions>>? configure = null)
            where TContext : class
        {
            var options = Options.Create(new SessionStateTableOptions());
            configure?.Invoke(options);

            modelBuilder.Entity<SessionStateTable<TContext, TContextKey>>(builder =>
            {
                builder.ToTable(options.Value.SessionStateTableName);
            });
            modelBuilder.Entity<SessionEventAwaiterTable<TContext, TContextKey>>(builder =>
            {
                builder.ToTable(options.Value.SessionEventAwaiterTableName);
                builder.HasIndex(aw => new { aw.IsActive, aw.Identifier }).HasFilter("[IsActive] = 1");
            });
            modelBuilder.Entity<TContext>();
        }
    }
}
