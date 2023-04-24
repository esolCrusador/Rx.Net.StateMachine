using Rx.Net.StateMachine.Extensions;
using Rx.Net.StateMachine.Persistance.Entities;
using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Storage;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Persistance
{
    public class WorkflowManager<TSessionState, TContext>
        where TSessionState : SessionStateBaseEntity
    {
        private readonly ISessionStateContextConnector<TSessionState, TContext> _sessionStateContext;
        private readonly JsonSerializerOptions _options;
        private readonly ISessionStateUnitOfWorkFactory<TSessionState> _uofFactory;
        private readonly IWorkflowResolver _workflowResolver;

        public StateMachine StateMachine { get; }

        public WorkflowManager(ISessionStateContextConnector<TSessionState, TContext> sessionStateContext, JsonSerializerOptions options, ISessionStateUnitOfWorkFactory<TSessionState> uofFactory, IWorkflowResolver workflowResolver)
            : this(sessionStateContext, options, uofFactory, workflowResolver, new StateMachine(options))
        { }

        public WorkflowManager(ISessionStateContextConnector<TSessionState, TContext> sessionStateContext, JsonSerializerOptions options, ISessionStateUnitOfWorkFactory<TSessionState> uofFactory, IWorkflowResolver workflowResolver, StateMachine stateMachine)
        {
            _sessionStateContext = sessionStateContext;
            _options = options;
            _uofFactory = uofFactory;
            _workflowResolver = workflowResolver;
            StateMachine = stateMachine;
        }

        public async Task<HandlingResult> StartHandle(string workflowId, TContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            using var uof = _uofFactory.Create();
            var newSessionStateEntity = CreateNewSessionState(workflowId, uof, context);
            var sessionState = ToSessionState(newSessionStateEntity, context);

            return await HandleSessionState(newSessionStateEntity, sessionState, context, uof);
        }

        public async Task<HandlingResult> StartHandle<TSource>(TSource source, string workflowId, TContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            using var uof = _uofFactory.Create();
            var newSessionStateEntity = CreateNewSessionState(workflowId, uof, context);
            var sessionState = ToSessionState(newSessionStateEntity, context);

            return await StartHandleSessionState(source, newSessionStateEntity, sessionState, context, uof);
        }

        public async Task<List<HandlingResult>> HandleEvent<TEvent>(TEvent @event, TContext context)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));

            using var uof = _uofFactory.Create();
            var eventType = SessionEventAwaiter.GetTypeName(@event.GetType());

            var contextFilter = _sessionStateContext.GetContextFilter(context);
            Expression<Func<TSessionState, bool>> awaiterFilter = ss =>
                ss.Status == SessionStateStatus.InProgress
                && ss.Awaiters.Any(aw => aw.TypeName == eventType);
            var filter = ExpressionExtensions.Aggregate(
                (matches1, matches2) => matches1 && matches2,
                contextFilter,
                awaiterFilter
            );

            var sessionStates = await uof.GetSessionStates(filter);

            if (sessionStates.Count == 0)
                return new List<HandlingResult>();

            List<HandlingResult> results = new List<HandlingResult>(sessionStates.Count);
            foreach (var ss in sessionStates)
                results.Add(await HandleSessionStateEvent(ss, context, @event, uof));

            return results;
        }

        private TSessionState CreateNewSessionState(string workflowId, ISessionStateUnitOfWork<TSessionState> uof, TContext context)
        {
            var sessionState = _sessionStateContext.CreateNewSessionState(context);
            sessionState.WorkflowId = workflowId;
            sessionState.Status = SessionStateStatus.Created;
            sessionState.Steps = new List<SessionStepEntity>();
            sessionState.Items = new List<SessionItemEntity>();
            sessionState.Awaiters = new List<SessionEventAwaiterEntity>();
            sessionState.Counter = 0;
            sessionState.PastEvents = new List<SessionEventEntity>();

            uof.Add(sessionState);

            return sessionState;
        }

        private async Task<HandlingResult> HandleSessionStateEvent<TEvent>(TSessionState sessionStateEntity, TContext context, TEvent @event, ISessionStateUnitOfWork<TSessionState> uof)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var sessionState = ToSessionState(sessionStateEntity, context);
            bool isAdded = StateMachine.AddEvent(sessionState, @event);
            if (!isAdded && sessionState.Status != SessionStateStatus.Created)
                return HandlingResult.Ignored;

            return await HandleSessionState(sessionStateEntity, sessionState, context, uof);
        }

        private async Task<HandlingResult> HandleSessionState(TSessionState sessionStateEntity, SessionState sessionState, TContext context, ISessionStateUnitOfWork<TSessionState> uof)
        {
            var storage = new SessionStateStorage(PersistStrategy.Default, st =>
            {
                UpdateSessionStateEntity(st, sessionStateEntity);
                return uof.Save();
            });

            var workflowFactory = await _workflowResolver.GetWorkflowFactory(sessionState.WorkflowId);
            return await StateMachine.HandleWorkflow(sessionState, storage, workflowFactory);
        }

        private async Task<HandlingResult> StartHandleSessionState<TSource>(TSource source, TSessionState sessionStateEntity, SessionState sessionState, TContext context, ISessionStateUnitOfWork<TSessionState> uof)
        {
            var storage = new SessionStateStorage(PersistStrategy.Default, st =>
            {
                UpdateSessionStateEntity(st, sessionStateEntity);
                return uof.Save();
            });

            var workflowFactory = await _workflowResolver.GetWorkflowFactory<TSource, Unit>(sessionState.WorkflowId);
            return await StateMachine.StartHandleWorkflow(source, sessionState, storage, workflowFactory);
        }

        private static SessionState ToSessionState(TSessionState entity, object context)
        {
            return new SessionState(
                entity.WorkflowId,
                context,
                entity.Counter,
                entity.Steps.ToDictionary(s => s.Id, s => new SessionStateStep(s.State, s.SequenceNumber)),
                entity.Items.ToDictionary(i => i.Id, i => i.Value),
                MapSessionEvents(entity.PastEvents),
                entity.Awaiters.Select(aw => new SessionEventAwaiter(aw.AwaiterId, aw.TypeName, aw.SequenceNumber)).ToList()
            )
            {
                Status = entity.Status,
                Result = entity.Result
            };
        }

        private static void UpdateSessionStateEntity(SessionState state, TSessionState dest)
        {
            dest.WorkflowId = state.WorkflowId;
            dest.Steps = state.Steps.Select(kvp =>
                    new SessionStepEntity
                    {
                        Id = kvp.Key,
                        State = kvp.Value.State,
                        SequenceNumber = kvp.Value.SequenceNumber
                    }).ToList();
            dest.Items = state.Items.Select(kvp =>
                    new SessionItemEntity
                    {
                        Id = kvp.Key,
                        Value = kvp.Value
                    }).ToList();
            dest.PastEvents = MapSessionEventEntities(state.PastEvents);
            dest.Awaiters = state.SessionEventAwaiters.Select(aw => new SessionEventAwaiterEntity
            {
                AwaiterId = aw.AwaiterId,
                TypeName = aw.TypeName,
                SequenceNumber = aw.SequenceNumber
            }).ToList();
            dest.Counter = state.Counter;
            dest.Status = state.Status;
            dest.Result = state.Result;
        }

        private static List<PastSessionEvent> MapSessionEvents(IEnumerable<SessionEventEntity> events)
        {
            return events.Select(se => new PastSessionEvent(se.SequenceNumber, se.Event, se.EventType, se.Awaiters, se.Handled)).ToList();
        }

        private static List<SessionEventEntity> MapSessionEventEntities(IEnumerable<PastSessionEvent> events)
        {
            return events.Select(e => new SessionEventEntity
            {
                Event = e.SerializedEvent,
                EventType = e.EventType,
                SequenceNumber = e.SequenceNumber,
                Awaiters = e.Awaiters,
                Handled = e.Handled
            }).ToList();
        }


    }
}
