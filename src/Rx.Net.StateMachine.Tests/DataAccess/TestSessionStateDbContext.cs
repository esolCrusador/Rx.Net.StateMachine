using Microsoft.EntityFrameworkCore;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.EntityFramework.Extensions;
using Rx.Net.StateMachine.Tests.Concurrency;
using Rx.Net.StateMachine.Tests.Entities;
using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.DataAccess
{
    public class TestSessionStateDbContext : DbContext
    {
        private readonly IDisposable? _concurrencyHandler;
        private readonly GlobalContextState _globalContextState;

        public DbSet<TaskEntity> Tasks { get; set; }
        public DbSet<TaskCommentEntity> TaskComments { get; set; }
        public DbSet<UserContext> Contexts { get; set; }
        public TestSessionStateDbContext(GlobalContextState globalContextState, DbContextOptions options) : base(options)
        {
            _globalContextState = globalContextState;
            _concurrencyHandler = this.HandleConcurrency();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ConfigureSessions<UserContext, int>();

            modelBuilder.Entity<UserContext>(builder =>
            {
                builder.HasIndex(uc => new { uc.BotId, uc.ChatId }).IsUnique();
            });

            modelBuilder.Entity<TaskEntity>(builder =>
            {
                builder.HasOne(t => t.Assignee)
                .WithMany(a => a.AssignedTasks)
                .OnDelete(DeleteBehavior.NoAction);
                builder.HasOne(t => t.Supervisor)
                .WithMany(s => s.SupervisedTasks)
                .OnDelete(DeleteBehavior.NoAction);
            });
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _globalContextState.Execute();

            return await base.SaveChangesAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _concurrencyHandler?.Dispose();
            base.Dispose();
        }

        public override ValueTask DisposeAsync()
        {
            _concurrencyHandler?.Dispose();
            return base.DisposeAsync();
        }
    }
}
