using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Persistance.Entities;
using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Storage;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Persistance
{
    public class WorkflowManager<TContext>
    {
        private readonly ISessionStateUnitOfWorkFactory _uofFactory;
        private readonly IWorkflowResolver _workflowResolver;
        private readonly IEventAwaiterResolver _eventAwaiterResolver;

        public StateMachine StateMachine { get; }

        public WorkflowManager(ISessionStateUnitOfWorkFactory uofFactory, IWorkflowResolver workflowResolver, IEventAwaiterResolver eventAwaiterResolver, StateMachine stateMachine)
        {
            _uofFactory = uofFactory;
            _workflowResolver = workflowResolver;
            _eventAwaiterResolver = eventAwaiterResolver;
            StateMachine = stateMachine;
        }

        public async Task<HandlingResult> StartHandle(string workflowId, TContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            await using var uof = _uofFactory.Create();
            var newSessionStateEntity = CreateNewSessionState(workflowId, uof, context);
            var sessionState = ToSessionState(newSessionStateEntity);

            return await HandleSessionState(newSessionStateEntity, sessionState, uof);
        }

        public async Task<HandlingResult> StartHandle<TSource>(TSource source, string workflowId, TContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            await using var uof = _uofFactory.Create();
            var newSessionStateEntity = CreateNewSessionState(workflowId, uof, context);
            var sessionState = ToSessionState(newSessionStateEntity);

            return await StartHandleSessionState(source, newSessionStateEntity, sessionState, context, uof);
        }

        public async Task RemoveDefaultSesssions(Guid? newDefaultSessionId, string userContextId)
        {
            await HandleEvent(new DefaultSessionRemoved { SessionId = newDefaultSessionId, UserContextId = userContextId });
        }

        public async Task<List<HandlingResult>> HandleEvent<TEvent>(TEvent @event)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));

            await using var uof = _uofFactory.Create();

            var sessionStates = await uof.GetSessionStates(@event);

            if (sessionStates.Count == 0)
                return new List<HandlingResult>();

            List<HandlingResult> results = new List<HandlingResult>(sessionStates.Count);
            foreach (var ss in sessionStates)
                results.Add(await HandleSessionStateEvent(ss, @event, uof));

            return results;
        }

        private SessionStateEntity CreateNewSessionState(string workflowId, ISessionStateUnitOfWork uof, TContext context)
        {
            var sessionState = new SessionStateEntity
            {
                SessionStateId = Guid.NewGuid(),
                WorkflowId = workflowId,
                Status = SessionStateStatus.Created,
                Steps = new List<SessionStepEntity>(),
                Items = new List<SessionItemEntity>(),
                Awaiters = new List<SessionEventAwaiterEntity>(),
                Counter = 0,
                PastEvents = new List<SessionEventEntity>(),
                Context = context
            };

            uof.Add(sessionState);

            return sessionState;
        }

        private async Task<HandlingResult> HandleSessionStateEvent<TEvent>(SessionStateEntity sessionStateEntity, TEvent @event, ISessionStateUnitOfWork uof)
        {
            var sessionState = ToSessionState(sessionStateEntity);
            bool isAdded = StateMachine.AddEvent(sessionState, @event, _eventAwaiterResolver.GetEventAwaiters(@event));
            if (!isAdded && sessionState.Status != SessionStateStatus.Created)
                return HandlingResult.Ignored;

            return await HandleSessionState(sessionStateEntity, sessionState, uof);
        }

        private async Task<HandlingResult> HandleSessionState(SessionStateEntity sessionStateEntity, SessionState sessionState, ISessionStateUnitOfWork uof)
        {
            var storage = new SessionStateStorage(PersistStrategy.Default, st =>
            {
                UpdateSessionStateEntity(st, sessionStateEntity);
                return uof.Save();
            });

            var workflowFactory = await _workflowResolver.GetWorkflow(sessionState.WorkflowId);
            return await StateMachine.HandleWorkflow(sessionState, storage, workflowFactory);
        }

        private async Task<HandlingResult> StartHandleSessionState<TSource>(TSource source, SessionStateEntity sessionStateEntity, SessionState sessionState, TContext context, ISessionStateUnitOfWork uof)
        {
            var storage = new SessionStateStorage(PersistStrategy.Default, st =>
            {
                UpdateSessionStateEntity(st, sessionStateEntity);
                return uof.Save();
            });

            var workflowFactory = await _workflowResolver.GetWorkflow<TSource, Unit>(sessionState.WorkflowId);
            return await StateMachine.StartHandleWorkflow(source, sessionState, storage, workflowFactory);
        }

        private static SessionState ToSessionState(SessionStateEntity entity)
        {
            return new SessionState(
                entity.SessionStateId,
                entity.WorkflowId,
                entity.Context,
                entity.IsDefault,
                entity.Counter,
                entity.Steps.ToDictionary(s => s.Id, s => new SessionStateStep(s.State, s.SequenceNumber)),
                entity.Items.ToDictionary(i => i.Id, i => i.Value),
                MapSessionEvents(entity.PastEvents),
                entity.Awaiters.Select(aw => new SessionEventAwaiter(aw.AwaiterId, aw.Name, aw.Identifier, aw.SequenceNumber)).ToList()
            )
            {
                Status = entity.Status,
                Result = entity.Result
            };
        }

        private static void UpdateSessionStateEntity(SessionState state, SessionStateEntity dest)
        {
            dest.WorkflowId = state.WorkflowId;
            dest.IsDefault = state.IsDefault;
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
                Name = aw.Name,
                Identifier = aw.Identifier,
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
