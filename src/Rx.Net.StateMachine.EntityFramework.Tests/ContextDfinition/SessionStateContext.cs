using Microsoft.EntityFrameworkCore;
using Rx.Net.StateMachine.EntityFramework.Tests.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.EntityFramework.Tests.ContextDfinition
{
    public class SessionStateContext: DbContext
    {
        public DbSet<SessionStateTable> SessionStates { get; set; }
        public DbSet<SessionStepTable> SessionStateSteps { get; set; }
        public DbSet<SessionItemTable> SessionStateItems { get; set; }
        public DbSet<SessionEventAwaiterTable> SessionStateEventAwaiters { get; set; }
        public DbSet<SessionEventTable> SessionStateEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SessionStateTable>(builder =>
            {
                builder.HasKey(ss => ss.SessionStateId);
                builder.Property(ss => ss.Result).HasMaxLength(1024);

                builder.HasMany(ss => ss.Steps)
                    .WithOne()
                    .HasForeignKey(ss => ss.SessionStateId);
                builder.HasMany(ss => ss.Items)
                    .WithOne()
                    .HasForeignKey(ss => ss.SessionStateId);
                builder.HasMany(ss => ss.PastEvents)
                    .WithOne()
                    .HasForeignKey(ss => ss.SessionStateId);
                builder.HasMany(ss => ss.Awaiters)
                    .WithOne()
                    .HasForeignKey(ss => ss.SessionStateId);
            });

            modelBuilder.Entity<SessionStepTable>(builder =>
            {
                builder.HasKey(s => s.Id);
                builder.Property(s => s.Id).HasMaxLength(128);
                builder.Property(s => s.State).HasMaxLength(2048);
            });

            modelBuilder.Entity<SessionItemTable>(builder =>
            {
                builder.HasKey(s => s.Id);
                builder.Property(s => s.Id).HasMaxLength(128);
                builder.Property(s => s.Value).HasMaxLength(2048);
            });

            modelBuilder.Entity<SessionEventTable>(builder =>
            {
                builder.HasKey(s => s.Id);
                builder.Property(s => s.Event).HasMaxLength(2048);
                builder.Property(s => s.EventType).HasMaxLength(256);
            });

            modelBuilder.Entity<SessionEventAwaiterTable>(builder =>
            {
                builder.HasKey(s => s.AwaiterId);
                builder.Property(s => s.TypeName).HasMaxLength(256);
            });
        }
    }
}
