﻿using Microsoft.EntityFrameworkCore;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.EntityFramework.Extensions;
using Rx.Net.StateMachine.EntityFramework.Tables;
using Rx.Net.StateMachine.EntityFramework.Tests.Tables;
using Rx.Net.StateMachine.Persistance;
using Rx.Net.StateMachine.Persistance.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.EntityFramework.Tests.UnitOfWork
{
    public abstract class EFSessionStateUnitOfWork<TContext, TContextKey> : ISessionStateUnitOfWork
        where TContext : class
    {
        record SessionStateData
        {
            public SessionStateData(SessionStateEntity sessionState, SessionStateTable<TContext, TContextKey> row)
            {
                SessionState = sessionState;
                Row = row;
            }

            public SessionStateEntity SessionState { get; }
            public SessionStateTable<TContext, TContextKey> Row { get; }
        }
        private readonly Dictionary<Guid, SessionStateData> _loadedSessionStates;
        private SessionStateDbContext<TContext, TContextKey>? _sessionStateContext;

        protected internal SessionStateDbContext<TContext, TContextKey> SessionStateDbContext
        {
            get => _sessionStateContext ?? throw new ArgumentException($"{nameof(SessionStateDbContext)} is not initialized");
            set => _sessionStateContext = value;
        }

        public EFSessionStateUnitOfWork()
        {
            _loadedSessionStates = new Dictionary<Guid, SessionStateData>();
        }

        public Task Add(SessionStateEntity sessionState)
        {
            var row = new SessionStateTable<TContext, TContextKey>
            {
                SessionStateId = sessionState.SessionStateId,
                Awaiters = new()
            };
            Map(sessionState, row);
            _loadedSessionStates.Add(row.SessionStateId, new SessionStateData(sessionState, row));
            SessionStateDbContext.SessionStates.Add(row);
            SessionStateDbContext.Contexts.Add(row.Context);

            return Task.CompletedTask;
        }

        public async Task<IReadOnlyCollection<SessionStateEntity>> GetSessionStates(object @event)
        {
            var sessions = await SessionStateDbContext.SessionStates
                .Include(ss => ss.Context)
                .Include(ss => ss.Awaiters)
                .Where(GetFilter(@event))
                .ToListAsync();

            return MapToSessionStates(sessions);
        }

        protected abstract Expression<Func<SessionStateTable<TContext, TContextKey>, bool>> GetFilter(object @event);

        public void Dispose() => SessionStateDbContext.Dispose();

        public ValueTask DisposeAsync() => SessionStateDbContext.DisposeAsync();

        public async Task Save()
        {
            foreach (var pair in _loadedSessionStates.Values)
                Map(pair.SessionState, pair.Row);

            await SessionStateDbContext.SaveChangesAsync();
        }

        private void Map(SessionStateTable<TContext, TContextKey> source, SessionStateEntity dest)
        {
            dest.WorkflowId = source.WorkflowId;
            dest.Counter = source.Counter;
            dest.Steps = JsonSerializer.Deserialize<List<SessionStepEntity>>(source.Steps)
                ?? throw new ArgumentException("Steps must be not null");
            dest.Items = JsonSerializer.Deserialize<List<SessionItemEntity>>(source.Items)
                ?? throw new ArgumentException("Items must be not null");
            dest.PastEvents = JsonSerializer.Deserialize<List<SessionEventEntity>>(source.PastEvents)
                ?? throw new ArgumentException("PastEvents must be not null");
            dest.Awaiters = source.Awaiters.Select(aw => new SessionEventAwaiterEntity
            {
                AwaiterId = aw.AwaiterId,
                SequenceNumber = aw.SequenceNumber,
                TypeName = aw.TypeName
            }).ToList();

            dest.Status = source.Status;
            dest.Result = source.Result;
            dest.Context = source.Context;
        }

        private void Map(SessionStateEntity source, SessionStateTable<TContext, TContextKey> dest)
        {
            dest.WorkflowId = source.WorkflowId;
            dest.Counter = source.Counter;

            dest.Steps = JsonSerializer.Serialize(source.Steps);
            dest.Items = JsonSerializer.Serialize(source.Items);
            dest.PastEvents = JsonSerializer.Serialize(source.PastEvents);
            dest.Awaiters.ToList().JoinTo(source.Awaiters)
                .LeftKey(db => db.AwaiterId)
                .RightKey(e => e.AwaiterId)
                .Merge()
                .Delete(db =>
                {
                    SessionStateDbContext.Remove(db);
                    dest.Awaiters.Remove(db);
                })
                .Create(aw =>
                {
                    var awaiter = new SessionEventAwaiterTable<TContext, TContextKey>
                    {
                        AwaiterId = aw.AwaiterId,
                        SessionStateId = source.SessionStateId,
                        SequenceNumber = aw.SequenceNumber,
                        TypeName = aw.TypeName,
                        Context = (TContext)source.Context
                    };
                    SessionStateDbContext.Add(awaiter);
                    dest.Awaiters.Add(awaiter);
                })
                .Execute();

            dest.Status = source.Status;
            dest.Result = source.Result;
            dest.Context = (TContext)source.Context;
        }

        protected IReadOnlyCollection<SessionStateEntity> MapToSessionStates(IEnumerable<SessionStateTable<TContext, TContextKey>> source)
        {
            return source.Select(ss =>
            {
                var e = new SessionStateEntity();
                Map(ss, e);
                _loadedSessionStates.Add(ss.SessionStateId, new SessionStateData(e, ss));

                return e;
            }).ToList();
        }
    }
}
