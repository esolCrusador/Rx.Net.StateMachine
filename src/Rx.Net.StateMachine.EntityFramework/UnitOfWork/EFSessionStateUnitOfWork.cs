using Microsoft.EntityFrameworkCore;
using Rx.Net.StateMachine.EntityFramework.Awaiters;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.EntityFramework.Tables;
using Rx.Net.StateMachine.EntityFramework.UnitOfWork;
using Rx.Net.StateMachine.Extensions;
using Rx.Net.StateMachine.Persistance;
using Rx.Net.StateMachine.Persistance.Entities;
using Rx.Net.StateMachine.Persistance.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.EntityFramework.Tests.UnitOfWork
{
    public class EFSessionStateUnitOfWork<TContext, TContextKey> : ISessionStateUnitOfWork
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
        private SessionStateDbContextFactory? _contextFactory;
        private DbContext? _sessionStateContext;
        private ContextKeySelector<TContext, TContextKey>? _contextKeySelector;
        private AwaitHandlerResolver<TContext, TContextKey>? _eventAwaiterResolver;
        private JsonSerializerOptions? _jsonSerializerOptions;

        protected internal SessionStateDbContextFactory ContextFactory
        {
            get => _contextFactory ?? throw new ArgumentException($"{nameof(ContextFactory)} is not initialized");
            set => _contextFactory = value;
        }
        protected internal DbContext SessionStateDbContext
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
        protected internal JsonSerializerOptions JsonSerializerOptions
        {
            get => _jsonSerializerOptions ?? throw new ArgumentException($"{nameof(JsonSerializerOptions)} is not initialized");
            set => _jsonSerializerOptions = value;
        }

        public EFSessionStateUnitOfWork()
        {
        }

        public ISessionStateMemento Add(SessionStateEntity sessionState)
        {
            var row = new SessionStateTable<TContext, TContextKey>
            {
                SessionStateId = sessionState.SessionStateId,
                Awaiters = new(),
                CrearedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            row.ContextId = ContextKeySelector.GetContextKey((TContext)sessionState.Context);
            SessionStateDbContext.Set<SessionStateTable<TContext, TContextKey>>().Add(row);

            ISessionStateMemento result = new EFSessionStateMemento<TContext, TContextKey>(JsonSerializerOptions, SessionStateDbContext, sessionState, row);

            return result;
        }

        public async Task<IReadOnlyCollection<ISessionStateMemento>> GetSessionStates(object @event, CancellationToken cancellationToken)
        {
            var awaitHandler = EventAwaiterResolver.GetAwaiterHandler(@event.GetType());

            var filter = GetAwaitersFilter(awaitHandler, @event);
            var sessionId = GetSessionId(awaitHandler, @event);
            if (sessionId.HasValue)
                filter = ExpressionExtensions.Aggregate(
                    (r1, r2) => r1 || r2,
                    s => s.SessionStateId == sessionId.Value,
                    filter
                );

            var sessions = await SessionStateDbContext.Set<SessionStateTable<TContext, TContextKey>>()
                .Include(ss => ss.Context)
                .Include(ss => ss.Awaiters)
                .Where(filter)
                .Where(awaitHandler.GetSessionStateFilter(@event))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (sessionId.HasValue && !sessions.Exists(s => s.SessionStateId == sessionId.Value))
                throw new ConcurrencyException($"Session {sessionId.Value} was not persisted");

            return GetMemenots(sessions).ToList();
        }

        public async Task<IReadOnlyCollection<ISessionStateMemento>> GetSessionStates(IEnumerable<object> events, CancellationToken cancellationToken)
        {
            var awaitHandlers = events.Select(ev => new KeyValuePair<object, IAwaiterHandler<TContext, TContextKey>>(
                ev,
                EventAwaiterResolver.GetAwaiterHandler(ev.GetType()))
            );

            var sessionIds = new List<Guid>();
            var filterExpression = awaitHandlers.Select(kvp =>
            {
                var awaitHandler = kvp.Value;
                var ev = kvp.Key;

                var filter = GetAwaitersFilter(awaitHandler, ev);
                var sessionId = awaitHandler.GetStaleSessionVersion(ev)?.SessionId;
                if (sessionId.HasValue)
                {
                    sessionIds.Add(sessionId.Value);
                    filter = ExpressionExtensions.Aggregate(
                        (r1, r2) => r1 || r2,
                        s => s.SessionStateId == sessionId.Value,
                        filter
                    );
                }

                return ExpressionExtensions.Aggregate(
                    (match1, match2) => match1 && match2,
                    filter,
                    awaitHandler.GetSessionStateFilter(ev)
                );
            }).ToList().Aggregate((match1, match2) => match1 || match2);

            var sessions = await SessionStateDbContext.Set<SessionStateTable<TContext, TContextKey>>()
                .Include(ss => ss.Context)
                .Include(ss => ss.Awaiters)
                .Where(filterExpression)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (sessionIds.Count != 0)
            {
                var notFoundSessions = sessionIds.Where(sessionId => !sessions.Any(s => s.SessionStateId == sessionId))
                    .ToList();

                if (notFoundSessions.Count != 0)
                    throw new ConcurrencyException($"Sessions {string.Join(", ", notFoundSessions)} were not persisted");
            }

            return GetMemenots(sessions).ToList();
        }

        private IEnumerable<ISessionStateMemento> GetMemenots(IReadOnlyList<SessionStateTable<TContext, TContextKey>> rows)
        {
            for (int i = 0; i < rows.Count; i++)
                if (i == 0)
                    yield return CreateMemento(rows[i]);
                else
                    yield return CreateMemento(rows[i], _contextFactory);
        }

        public async Task<ISessionStateMemento?> GetSessionState(Guid sessionStateId, CancellationToken cancellationToken)
        {
            var session = await SessionStateDbContext.Set<SessionStateTable<TContext, TContextKey>>()
                .Include(ss => ss.Context)
                .Include(ss => ss.Awaiters)
                .FirstOrDefaultAsync(ss => ss.SessionStateId == sessionStateId, cancellationToken);

            if (session == null)
                return null;

            return CreateMemento(session);
        }

        private Expression<Func<SessionStateTable<TContext, TContextKey>, bool>> GetAwaitersFilter(IAwaiterHandler<TContext, TContextKey> awaiterHandler, object @event)
        {
            var awaiterIdentifiers = awaiterHandler.GetAwaiterIdTypes()
                .Select(at => AwaiterExtensions.CreateAwaiter(at, @event).AwaiterId).ToList();

            return ss => ss.Awaiters.Any(aw => aw.IsActive && awaiterIdentifiers.Contains(aw.Identifier));
        }

        private Guid? GetSessionId(IAwaiterHandler<TContext, TContextKey> awaiterHandler, object @event) =>
            awaiterHandler.GetStaleSessionVersion(@event)?.SessionId;

        private EFSessionStateMemento<TContext, TContextKey> CreateMemento(SessionStateTable<TContext, TContextKey> row, SessionStateDbContextFactory? dbContextFactory = default)
        {
            var entity = new SessionStateEntity();
            Map(row, entity);

            return dbContextFactory == null
                ? new EFSessionStateMemento<TContext, TContextKey>(JsonSerializerOptions, SessionStateDbContext, entity, row)
                : new EFSessionStateMemento<TContext, TContextKey>(JsonSerializerOptions, dbContextFactory, entity, row);
        }

        public void Dispose() => SessionStateDbContext.Dispose();

        public ValueTask DisposeAsync() => SessionStateDbContext.DisposeAsync();

        private void Map(SessionStateTable<TContext, TContextKey> source, SessionStateEntity dest)
        {
            dest.SessionStateId = source.SessionStateId;
            dest.WorkflowId = source.WorkflowId;
            dest.Counter = source.Counter;
            dest.IsDefault = source.IsDefault;
            dest.Steps = JsonSerializer.Deserialize<List<SessionStepEntity>>(source.Steps, JsonSerializerOptions)
                ?? throw new ArgumentException("Steps must be not null");
            dest.Items = JsonSerializer.Deserialize<List<SessionItemEntity>>(source.Items, JsonSerializerOptions)
                ?? throw new ArgumentException("Items must be not null");
            dest.PastEvents = JsonSerializer.Deserialize<List<SessionEventEntity>>(source.PastEvents, JsonSerializerOptions)
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
    }
}
