using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Options;
using Rx.Net.StateMachine.EntityFramework.Tables;
using Rx.Net.StateMachine.EntityFramework.Tests.Tables;
using System;

namespace Rx.Net.StateMachine.EntityFramework.ContextDfinition
{
    public class SessionStateTableOptions<TContext, TContextKey>
    {
        public string SessionStateTableName { get; set; } = "SessionStates";
        public string SessionEventAwaiterTableName { get; set; } = "SessionStateAwaiters";
        public Func<string, string> EscapePropertyName { get; set; } = name => $"[{name}]";
        public Action<EntityTypeBuilder<SessionStateTable<TContext, TContextKey>>> ConfigureSessionStateTable { get; set; } = _ => { };
    }
    public static class SessionStateDbContextExtensions
    {
        public static void ConfigureSessions<TContext, TContextKey>(this ModelBuilder modelBuilder, Action<SessionStateTableOptions<TContext, TContextKey>>? configure = null)
            where TContext : class
        {
            var options = new SessionStateTableOptions<TContext, TContextKey>();
            configure?.Invoke(options);

            modelBuilder.Entity<SessionStateTable<TContext, TContextKey>>(builder =>
            {
                builder.ToTable(options.SessionStateTableName);
                builder.HasIndex(i => new { i.Status, i.UpdatedAt, i.WorkflowId });
            });
            options.ConfigureSessionStateTable(modelBuilder.Entity<SessionStateTable<TContext, TContextKey>>());
            modelBuilder.Entity<SessionEventAwaiterTable<TContext, TContextKey>>(builder =>
            {
                builder.ToTable(options.SessionEventAwaiterTableName);
                builder.HasIndex(aw => new { aw.IsActive, aw.Identifier })
                    .HasFilter($"{options.EscapePropertyName(nameof(SessionEventAwaiterTable<TContext, TContextKey>.IsActive))} = 1");
            });
            modelBuilder.Entity<TContext>();
        }
    }
}
