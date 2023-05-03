using Microsoft.EntityFrameworkCore;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.Tests.Persistence;

namespace Rx.Net.StateMachine.Tests
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
        }
    }
}
