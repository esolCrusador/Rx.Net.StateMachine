using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Storage;
using Rx.Net.StateMachine.Tests.Entities;
using Rx.Net.StateMachine.Tests.Persistence;
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
        private readonly Func<SessionStateEntity, Func<StateMachineScope, IObservable<Unit>>> _workflowResolver;
        private readonly StateMachine _stateMachine;

        public WorkflowManager(JsonSerializerOptions options, Func<SessionStateUnitOfWork> uofFactory,
            Func<SessionStateEntity, Func<StateMachineScope, IObservable<Unit>>> workflowResolver)
        {
            _options = options;
            _uofFactory = uofFactory;
            _workflowResolver = workflowResolver;
            _stateMachine = new StateMachine { SerializerOptions = new JsonSerializerOptions() };
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
            {
                var newSessionState = new SessionStateEntity
                {
                    UserId = userContext.UserId,
                    Status = SessionStateStatus.Created,
                    Steps = new List<SessionStepEntity>(),
                    Awaiters = new List<SessionEventAwaiterEntity>(),
                    Counter = 0,
                    PastEvents = new List<SessionEventEntity>(),
                    Result = null,
                    SessionId = Guid.NewGuid()
                };
                uof.Add(newSessionState);
                sessionStates.Add(newSessionState);
            }

            List<HandlingResult> results = new List<HandlingResult>();
            foreach (var ss in sessionStates)
                results.Add(await HandleSessionState(ss, userContext, @event, uof));

            return results;
        }

        private async Task<HandlingResult> HandleSessionState<TEvent>(SessionStateEntity sessionStateEntity, object context, TEvent @event, SessionStateUnitOfWork uof)
        {
            var sessionState = ToSessionState(sessionStateEntity, context);
            bool isAdded = _stateMachine.AddEvent(sessionState, @event);
            if (!isAdded && sessionState.Status != SessionStateStatus.Created)
                return HandlingResult.Ignored;

            var storage = new SessionStateStorage(PersistStrategy.Default, st =>
            {
                UpdateSessionStateEntity(st, sessionStateEntity);
                return uof.Save();
            });

            var workflowFactory = _workflowResolver.Invoke(sessionStateEntity);
            return await _stateMachine.HandleWorkflow(sessionState, storage, workflowFactory);
        }

        private static SessionState ToSessionState(SessionStateEntity entity, object context)
        {
            return new SessionState(
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
