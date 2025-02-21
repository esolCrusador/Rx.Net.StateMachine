using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;

namespace Rx.Net.StateMachine.EntityFramework.Extensions
{
    public static class InMemoryConcurrencyExtensions
    {
        public static IDisposable? HandleConcurrency(this DbContext dbContext)
        {
            if (dbContext.Database.IsRelational())
                return null;

            dbContext.ChangeTracker.DetectedEntityChanges += ChangeTracker_DetectedEntityChanges;
            return new Unsubscribe(() => dbContext.ChangeTracker.DetectedEntityChanges -= ChangeTracker_DetectedEntityChanges);
        }
        private static void ChangeTracker_DetectedEntityChanges(object? sender, DetectedEntityChangesEventArgs args)
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

        private class Unsubscribe : IDisposable
        {
            private readonly Action _unsubscribe;

            public Unsubscribe(Action unsubscribe) => _unsubscribe = unsubscribe;

            public void Dispose()
            {
                GC.SuppressFinalize(this);
                _unsubscribe();
            }
        }
    }
}
