using Microsoft.EntityFrameworkCore;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.EntityFramework.Extensions;
using Rx.Net.StateMachine.EntityFramework.Tables;
using Rx.Net.StateMachine.EntityFramework.Tests.Tables;
using Rx.Net.StateMachine.Extensions;
using Rx.Net.StateMachine.Persistance;
using Rx.Net.StateMachine.Persistance.Entities;
using Rx.Net.StateMachine.Persistance.Exceptions;
using Rx.Net.StateMachine.States;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.EntityFramework.UnitOfWork
{
    public class EFSessionStateMemento<TContext, TContextKey> : ISessionStateMemento
    {
        private readonly DbContext? _dbContext;
        private readonly SessionStateDbContextFactory? _dbContextFactory;

        private readonly SessionStateTable<TContext, TContextKey> _row;
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public SessionStateEntity Entity { get; }

        public EFSessionStateMemento(JsonSerializerOptions jsonSerializerOptions, DbContext dbContext, SessionStateEntity entity, SessionStateTable<TContext, TContextKey> row)
        {
            _jsonSerializerOptions = jsonSerializerOptions;
            _dbContext = dbContext;
            Entity = entity;
            _row = row;
        }

        public EFSessionStateMemento(JsonSerializerOptions jsonSerializerOptions, SessionStateDbContextFactory dbContextFactory, SessionStateEntity entity, SessionStateTable<TContext, TContextKey> row)
        {
            _jsonSerializerOptions = jsonSerializerOptions;
            _dbContextFactory = dbContextFactory;
            Entity = entity;
            _row = row;
        }

        public async Task Save(CancellationToken cancellationToken)
        {
            if (_dbContext != null)
                await Save(_dbContext, cancellationToken);
            else
            {
                await using var dbContext = (_dbContextFactory ?? throw new ArgumentException($"{nameof(_dbContextFactory)} was not initialized")).CreateBase();

                await Save(dbContext, cancellationToken);
            }
        }

        private async Task Save(DbContext dbContext, CancellationToken cancellationToken)
        {
            if (dbContext.Entry(_row)?.State != EntityState.Added)
                dbContext.Set<SessionStateTable<TContext, TContextKey>>().Attach(_row);

            Map(dbContext, Entity, _row);

            var changedEntities = dbContext.ChangeTracker.Entries<SessionStateTable<TContext, TContextKey>>().Where(e => e.State == EntityState.Modified);
            foreach (var changed in changedEntities)
                changed.Entity.UpdatedAt = DateTimeOffset.UtcNow;

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new ConcurrencyException($"Concurrency during updating {_row.SessionStateId}", ex);
            }
        }

        private void Map(DbContext dbContext, SessionStateEntity source, SessionStateTable<TContext, TContextKey> dest)
        {
            dest.WorkflowId = source.WorkflowId;
            dest.Counter = source.Counter;
            dest.IsDefault = source.IsDefault;
            dest.Steps = JsonSerializer.Serialize(source.Steps, _jsonSerializerOptions);
            dest.Items = JsonSerializer.Serialize(source.Items, _jsonSerializerOptions);
            dest.PastEvents = JsonSerializer.Serialize(source.PastEvents, _jsonSerializerOptions);
            dest.Awaiters.ToList().JoinTo(source.Awaiters)
                .LeftKey(db => db.AwaiterId)
                .RightKey(e => e.AwaiterId)
                .Merge()
                .Delete(db =>
                {
                    dbContext.Remove(db);
                    dest.Awaiters.Remove(db);
                })
                .Update((db, aw) => db.IsActive = dest.Status == SessionStateStatus.InProgress)
                .Create(aw =>
                {
                    var awaiter = new SessionEventAwaiterTable<TContext, TContextKey>
                    {
                        SessionStateId = source.SessionStateId,
                        SequenceNumber = aw.SequenceNumber,
                        Name = aw.Name,
                        Identifier = aw.Identifier,
                        IgnoreIdentifier = aw.IgnoreIdentifier,
                        ContextId = dest.ContextId,
                        IsActive = dest.Status == SessionStateStatus.InProgress
                    };
                    dbContext.Add(awaiter);
                    dest.Awaiters.Add(awaiter);
                })
                .Execute();

            dest.Status = source.Status;
            dest.Result = source.Result;
        }
    }
}
