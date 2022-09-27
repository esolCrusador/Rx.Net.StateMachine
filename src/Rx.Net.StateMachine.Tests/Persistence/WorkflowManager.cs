using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Storage;
using Rx.Net.StateMachine.Tests.Entities;
using Rx.Net.StateMachine.Tests.Persistence;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests
{
    public partial class WorkflowManager
    {
        private readonly JsonSerializerOptions _options;
        private readonly Func<SessionStateUnitOfWork> _uofFactory;
        private readonly IWorkflowResolver _workflowResolver;
        private readonly StateMachine _stateMachine;

        public WorkflowManager(JsonSerializerOptions options, Func<SessionStateUnitOfWork> uofFactory, IWorkflowResolver workflowResolver)
        {
            _options = options;
            _uofFactory = uofFactory;
            _workflowResolver = workflowResolver;
            _stateMachine = new StateMachine { SerializerOptions = new JsonSerializerOptions() };
        }

        public async Task<HandlingResult> StartHandle(string workflowId, UserContext userContext)
        {
            using var uof = _uofFactory();
            var newSessionStateEntity = CreateNewSessionState(workflowId, uof, userContext);
            var sessionState = ToSessionState(newSessionStateEntity, userContext);

            return await HandleSessionState(newSessionStateEntity, sessionState, userContext, uof);
        }

        public async Task<HandlingResult> StartHandle<TSource>(TSource source, string workflowId, UserContext userContext)
        {
            using var uof = _uofFactory();
            var newSessionStateEntity = CreateNewSessionState(workflowId, uof, userContext);
            var sessionState = ToSessionState(newSessionStateEntity, userContext);

            return await StartHandleSessionState(source, newSessionStateEntity, sessionState, userContext, uof);
        }

        public async Task<List<HandlingResult>> HandleEvent<TEvent>(TEvent @event, UserContext userContext)
        {
            using var uof = _uofFactory();
            var eventType = SessionEventAwaiter.GetTypeName(@event.GetType());

            var sessionStates = uof.GetSessionStates(ss =>
                ss.UserId == userContext.UserId
                && ss.Status == SessionStateStatus.InProgress
                && ss.Awaiters.Any(aw => aw.TypeName == eventType)
            ).ToList();

            if (sessionStates.Count == 0)
                return new List<HandlingResult>();

            List<HandlingResult> results = new List<HandlingResult>(sessionStates.Count);
            foreach (var ss in sessionStates)
                results.Add(await HandleSessionStateEvent(ss, userContext, @event, uof));

            return results;
        }

        private SessionStateEntity CreateNewSessionState(string workflowId, SessionStateUnitOfWork uof, UserContext userContext)
        {
            var newSessionState = new SessionStateEntity
            {
                UserId = userContext.UserId,
                WorkflowId = workflowId,
                Status = SessionStateStatus.Created,
                Steps = new List<SessionStepEntity>(),
                Awaiters = new List<SessionEventAwaiterEntity>(),
                Counter = 0,
                PastEvents = new List<SessionEventEntity>(),
                Result = null,
                SessionId = Guid.NewGuid()
            };
            uof.Add(newSessionState);

            return newSessionState;
        }

        private async Task<HandlingResult> HandleSessionStateEvent<TEvent>(SessionStateEntity sessionStateEntity, object context, TEvent @event, SessionStateUnitOfWork uof)
        {
            var sessionState = ToSessionState(sessionStateEntity, context);
            bool isAdded = _stateMachine.AddEvent(sessionState, @event);
            if (!isAdded && sessionState.Status != SessionStateStatus.Created)
                return HandlingResult.Ignored;

            return await HandleSessionState(sessionStateEntity, sessionState, context, uof);
        }

        private async Task<HandlingResult> HandleSessionState(SessionStateEntity sessionStateEntity, SessionState sessionState, object context, SessionStateUnitOfWork uof)
        {
            var storage = new SessionStateStorage(PersistStrategy.Default, st =>
            {
                UpdateSessionStateEntity(st, sessionStateEntity);
                return uof.Save();
            });

            var workflowFactory = await _workflowResolver.GetWorkflowFactory(sessionState.WorkflowId);
            return await _stateMachine.HandleWorkflow(sessionState, storage, workflowFactory);
        }

        private async Task<HandlingResult> StartHandleSessionState<TSource>(TSource source, SessionStateEntity sessionStateEntity, SessionState sessionState, object context, SessionStateUnitOfWork uof)
        {
            var storage = new SessionStateStorage(PersistStrategy.Default, st =>
            {
                UpdateSessionStateEntity(st, sessionStateEntity);
                return uof.Save();
            });

            var workflowFactory = await _workflowResolver.GetWorkflowFactory<TSource, Unit>(sessionState.WorkflowId);
            return await _stateMachine.StartHandleWorkflow(source, sessionState, storage, workflowFactory);
        }

        private static SessionState ToSessionState(SessionStateEntity entity, object context)
        {
            return new SessionState(
                entity.WorkflowId,
                context,
                entity.Counter,
                entity.Steps.ToDictionary(s => s.Id, s => new SessionStateStep(s.State, s.SequenceNumber)),
                MapSessionEvents(entity.PastEvents),
                entity.Awaiters.Select(aw => new SessionEventAwaiter(aw.AwaiterId, aw.TypeName, aw.SequenceNumber)).ToList()
            )
            {
                Status = entity.Status,
                Result = entity.Result
            };
        }

        private static void UpdateSessionStateEntity(SessionState state, SessionStateEntity dest)
        {
            dest.WorkflowId = state.WorkflowId;
            dest.Steps = state.Steps.Select(kvp =>
                    new SessionStepEntity
                    {
                        Id = kvp.Key,
                        State = kvp.Value.State,
                        SequenceNumber = kvp.Value.SequenceNumber
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
