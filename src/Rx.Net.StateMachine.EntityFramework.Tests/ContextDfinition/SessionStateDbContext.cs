using Microsoft.EntityFrameworkCore;
using Rx.Net.StateMachine.EntityFramework.Tests.Tables;

namespace Rx.Net.StateMachine.EntityFramework.Tests.ContextDfinition
{
    public abstract class SessionStateDbContext<TContext, TContextKey> : DbContext
        where TContext : class
    {
        public SessionStateDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<SessionStateTable<TContext, TContextKey>> SessionStates { get; set; }
        public DbSet<SessionEventAwaiterTable<TContext, TContextKey>> SessionStateEventAwaiters { get; set; }
        public DbSet<TContext> Contexts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SessionStateTable<TContext, TContextKey>>(builder =>
            {
                builder.HasKey(ss => ss.SessionStateId);
                builder.Property(ss => ss.Result).HasMaxLength(1024);

                builder.HasMany(ss => ss.Awaiters)
                    .WithOne()
                    .HasForeignKey(ss => ss.SessionStateId)
                    .OnDelete(DeleteBehavior.NoAction);
                builder.HasOne(ss => ss.Context)
                    .WithMany()
                    .HasForeignKey(ss => ss.ContextId);
            });

            modelBuilder.Entity<SessionEventAwaiterTable<TContext, TContextKey>>(builder =>
            {
                builder.HasKey(s => s.AwaiterId);
                builder.Property(s => s.TypeName).HasMaxLength(256);
                builder.HasOne(aw => aw.Context)
                    .WithMany()
                    .HasForeignKey(aw => aw.ContextId);
            });
        }
    }
}
