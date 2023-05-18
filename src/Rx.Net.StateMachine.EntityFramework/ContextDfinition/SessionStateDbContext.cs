using Microsoft.EntityFrameworkCore;
using Rx.Net.StateMachine.EntityFramework.Tables;
using Rx.Net.StateMachine.EntityFramework.Tests.Tables;
using Rx.Net.StateMachine.Persistance.Exceptions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.EntityFramework.ContextDfinition
{
    public abstract class SessionStateDbContext<TContext, TContextKey> : DbContext
        where TContext : class
    {
        private GlobalContextState? _globalContextState;

        public SessionStateDbContext(DbContextOptions options) : base(options)
        {
            if (!Database.IsRelational())
                ChangeTracker.DetectedEntityChanges += ChangeTracker_DetectedEntityChanges;
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
                builder.Property(s => s.Identifier);
                builder.HasOne(aw => aw.Context)
                    .WithMany()
                    .HasForeignKey(aw => aw.ContextId);
                builder.HasIndex(aw => new { aw.SessionStateId, aw.Name }).IsUnique();
                builder.HasIndex(aw => new { aw.IsActive, aw.Identifier }).HasFilter("[IsActive] = 1");
            });
        }

        public override ValueTask DisposeAsync()
        {
            if (!Database.IsRelational())
                ChangeTracker.DetectedEntityChanges -= ChangeTracker_DetectedEntityChanges;
            return base.DisposeAsync();
        }

        public override void Dispose()
        {
            if (!Database.IsRelational())
                ChangeTracker.DetectedEntityChanges -= ChangeTracker_DetectedEntityChanges;
            base.Dispose();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_globalContextState != null)
                await _globalContextState.Execute();

            try
            {
                return await base.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new ConcurrencyException(ex);
            }
        }

        internal void SetGlobalContextState(GlobalContextState globalContextState)
        {
            _globalContextState = globalContextState;
        }

        private void ChangeTracker_DetectedEntityChanges(object? sender, Microsoft.EntityFrameworkCore.ChangeTracking.DetectedEntityChangesEventArgs args)
        {
            if (args.Entry.State == EntityState.Added)
            {
                foreach (var prop in args.Entry.Properties)
                {
                    if (prop.Metadata.IsConcurrencyToken)
                        prop.CurrentValue = Guid.NewGuid().ToByteArray();
                }
            }
            else if (args.Entry.State == EntityState.Modified)
            {
                foreach (var prop in args.Entry.Properties)
                {
                    if (prop.Metadata.IsConcurrencyToken)
                        prop.CurrentValue = Guid.NewGuid().ToByteArray();
                }
            }
        }
    }
}
