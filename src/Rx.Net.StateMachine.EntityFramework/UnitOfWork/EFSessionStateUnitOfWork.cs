using Microsoft.EntityFrameworkCore;
using Rx.Net.StateMachine.EntityFramework.Awaiters;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.EntityFramework.Extensions;
using Rx.Net.StateMachine.EntityFramework.Tables;
using Rx.Net.StateMachine.EntityFramework.Tests.Tables;
using Rx.Net.StateMachine.EntityFramework.UnitOfWork;
using Rx.Net.StateMachine.Extensions;
using Rx.Net.StateMachine.Persistance;
using Rx.Net.StateMachine.Persistance.Entities;
using Rx.Net.StateMachine.States;
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
        private ContextKeySelector<TContext, TContextKey>? _contextKeySelector;
        private AwaitHandlerResolver<TContext, TContextKey>? _eventAwaiterResolver;

        protected internal SessionStateDbContext<TContext, TContextKey> SessionStateDbContext
        {
            get => _sessionStateContext ?? throw new ArgumentException($"{nameof(SessionStateDbContext)} is not initialized");
            set => _sessionStateContext = value;
        }
        protected internal ContextKeySelector<TContext, TContextKey> ContextKeySelector
        {
            get => _contextKeySelector ?? throw new ArgumentException($"{nameof(ContextKeySelector)} is not initialized");
            set => _contextKeySelector = value;
        }

        protected internal AwaitHandlerResolver<TContext, TContextKey> EventAwaiterResolver
        {
            get => _eventAwaiterResolver ?? throw new ArgumentException($"{nameof(EventAwaiterResolver)} is not initialized");
            set => _eventAwaiterResolver = value;
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
            row.ContextId = ContextKeySelector.GetContextKey((TContext)sessionState.Context);
            _loadedSessionStates.Add(row.SessionStateId, new SessionStateData(sessionState, row));
            SessionStateDbContext.SessionStates.Add(row);

            return Task.CompletedTask;
        }

        public async Task<IReadOnlyCollection<SessionStateEntity>> GetSessionStates(object @event)
        {
            var awaitHandler = EventAwaiterResolver.GetAwaiterHandler(@event.GetType());

            var sessions = await SessionStateDbContext.SessionStates
                .Include(ss => ss.Context)
                .Include(ss => ss.Awaiters)
                .Where(ss => ss.Status == SessionStateStatus.InProgress)
                .Where(awaitHandler.GetSessionStateFilter(@event))
                .Where(GetAwaitersFilter(awaitHandler, @event))
                .ToListAsync();

            return MapToSessionStates(sessions);
        }

        private Expression<Func<SessionStateTable<TContext, TContextKey>, bool>> GetAwaitersFilter(IAwaiterHandler<TContext, TContextKey> awaiterHandler, object @event)
        {
            var awaiterIdentifiers = awaiterHandler.GetAwaiterIdTypes()
                .Select(at => AwaiterExtensions.CreateAwaiter(at, @event).AwaiterId).ToList();

            return ss => ss.Awaiters.Any(aw => awaiterIdentifiers.Contains(aw.Identifier));
        }

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
            dest.SessionStateId = source.SessionStateId;
            dest.WorkflowId = source.WorkflowId;
            dest.Counter = source.Counter;
            dest.IsDefault = source.IsDefault;
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
                Name = aw.Name,
                Identifier = aw.Identifier,
            }).ToList();

            dest.Status = source.Status;
            dest.Result = source.Result;
            dest.Context = source.Context;
        }

        private void Map(SessionStateEntity source, SessionStateTable<TContext, TContextKey> dest)
        {
            dest.WorkflowId = source.WorkflowId;
            dest.Counter = source.Counter;
            dest.IsDefault = source.IsDefault;
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
                        Name = aw.Name,
                        Identifier = aw.Identifier,
                        ContextId = dest.ContextId
                    };
                    SessionStateDbContext.Add(awaiter);
                    dest.Awaiters.Add(awaiter);
                })
                .Execute();

            dest.Status = source.Status;
            dest.Result = source.Result;
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
