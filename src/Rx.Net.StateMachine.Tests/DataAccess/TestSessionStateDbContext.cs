using Microsoft.EntityFrameworkCore;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.Tests.Entities;
using Rx.Net.StateMachine.Tests.Persistence;

namespace Rx.Net.StateMachine.Tests.DataAccess
{
    public class TestSessionStateDbContext : SessionStateDbContext<UserContext, int>
    {
        public TestSessionStateDbContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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
    }
}
