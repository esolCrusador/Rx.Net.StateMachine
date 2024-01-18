using Microsoft.Extensions.Logging;
using Polly;
using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Persistance.Entities;
using Rx.Net.StateMachine.Persistance.Exceptions;
using Rx.Net.StateMachine.Persistance.Extensions;
using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Storage;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Persistance
{
    public class WorkflowManager<TContext>
        where TContext : class
    {
        private readonly AsyncPolicy _concurrencyRetry;
        private readonly ILogger<WorkflowManager<TContext>> _logger;
        private readonly ISessionStateUnitOfWorkFactory _uofFactory;
        private readonly IWorkflowResolver _workflowResolver;
        private readonly IEventAwaiterResolver _eventAwaiterResolver;

        public StateMachine StateMachine { get; }

        public WorkflowManager(ILogger<WorkflowManager<TContext>> logger, ISessionStateUnitOfWorkFactory uofFactory, IWorkflowResolver workflowResolver, IEventAwaiterResolver eventAwaiterResolver, StateMachine stateMachine)
        {
            _logger = logger;
            _uofFactory = uofFactory;
            _workflowResolver = workflowResolver;
            _eventAwaiterResolver = eventAwaiterResolver;
            StateMachine = stateMachine;
            _concurrencyRetry = Policy.Handle<ConcurrencyException>()
                .RetryForeverAsync(ex => _logger.LogWarning(ex.Message));
        }

        public struct WorkflowRunner
        {
            private readonly WorkflowManager<TContext> _workflowManager;
            private readonly IWorkflowResolver _workflowResolver;
            private readonly TContext _context;

            public WorkflowRunner(WorkflowManager<TContext> workflowManager, IWorkflowResolver workflowResolver, TContext context)
            {
                _workflowManager = workflowManager;
                _workflowResolver = workflowResolver;
                _context = context;
            }
            public async Task<HandlingResult> Workflow<TWorkflow>() where TWorkflow : IWorkflow =>
                await _workflowManager.StartHandle(await _workflowResolver.GetWorkflow<TWorkflow>(), _context);

            public async Task<HandlingResult> Workflow(string workflowId) =>
                await _workflowManager.StartHandle(await _workflowResolver.GetWorkflow(workflowId), _context);
        }

        public WorkflowRunner Start(TContext context) =>
            new WorkflowRunner(this, _workflowResolver, context);

        private async Task<HandlingResult> StartHandle(IWorkflow workflow, TContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            await using var uof = _uofFactory.Create();
            var sessionStateMemento = await CreateNewSessionState(workflow.WorkflowId, uof, context);
            var sessionState = ToSessionState(sessionStateMemento.Entity);

            return await HandleSessionState(sessionState, workflow, sessionStateMemento);
        }

        public struct WorkflowRunner<TSource>
        {
            private readonly WorkflowManager<TContext> _workflowManager;
            private readonly IWorkflowResolver _workflowResolver;
            private readonly TContext _context;
            private readonly TSource _source;

            public WorkflowRunner(WorkflowManager<TContext> workflowManager, IWorkflowResolver workflowResolver, TContext context, TSource source)
            {
                _workflowManager = workflowManager;
                _workflowResolver = workflowResolver;
                _context = context;
                _source = source;
            }
            public async Task<HandlingResult> Workflow<TWorkflow>() where TWorkflow : IWorkflow<TSource> =>
                await _workflowManager.StartHandle(_source, await _workflowResolver.GetWorkflow<TWorkflow>(), _context);

            public async Task<HandlingResult> Workflow(string workflowId) =>
                await _workflowManager.StartHandle(_source, (IWorkflow<TSource>)await _workflowResolver.GetWorkflow(workflowId), _context);
        }

        public WorkflowRunner<TSource> Start<TSource>(TContext context, TSource source) =>
            new WorkflowRunner<TSource>(this, _workflowResolver, context, source);

        private async Task<HandlingResult> StartHandle<TSource>(TSource source, IWorkflow<TSource> workflow, TContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            await using var uof = _uofFactory.Create();
            var sessionStateMemento = await CreateNewSessionState(workflow.WorkflowId, uof, context);
            var sessionState = ToSessionState(sessionStateMemento.Entity);

            return await StartHandleSessionState<TSource>(source, sessionStateMemento.Entity, sessionState, workflow, sessionStateMemento);
        }

        public async Task RemoveDefaultSesssions(Guid? newDefaultSessionId, string userContextId)
        {
            await HandleEvent(new DefaultSessionRemoved { SessionId = newDefaultSessionId, UserContextId = userContextId });
        }

        public async Task CancelSession(Guid sessionId, CancellationReason reason)
        {
            await HandleEvent(new BeforeSessionCancelled(sessionId, reason));

            var result = await HandleEvent(new SessionCancelled(sessionId, reason));
            if (result.Count > 1)
                throw new InvalidOperationException($"Invalid session {sessionId} handled {result.Count} times");

            if (result.Count == 1)
            {
                var status = result[0].Status;
                if (status != HandlingStatus.Ignored)
                    return;
            }

            await using var uof = _uofFactory.Create();
            var session = await uof.GetSessionState(sessionId) ?? throw new ArgumentException($"Could not find session {sessionId}");
            session.Entity.Status = SessionStateStatus.Cancelled;
            session.Entity.Result = $"Cancelled because {JsonSerializer.Serialize(result)}";
            _logger.LogWarning($"Could not finish session {sessionId}. Cancelling...");

            await session.Save();
        }

        public Task<List<HandlingResult>> HandleEvent<TEvent>(TEvent @event)
            where TEvent : class
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));

            return _concurrencyRetry.ExecuteAsync(async () =>
            {
                await using var uof = _uofFactory.Create();

                var sessionStates = await uof.GetSessionStates(@event);

                _logger.LogInformation("Found {SessionIds} for event {EventType}\r\n{Event}", sessionStates.Select(s => s.Entity.SessionStateId), @event, JsonSerializer.Serialize(@event));

                if (sessionStates.Count == 0)
                    return new List<HandlingResult>();

                var handlers = sessionStates.Select(ss => HandleSessionStateEvent(@event, ss));

                return await HandleExceptions(handlers);
            });
        }

        public Task<List<HandlingResult>> HandleEvents(IReadOnlyCollection<object> events)
        {
            if (events.Count == 0)
                throw new ArgumentException(nameof(events));

            return _concurrencyRetry.ExecuteAsync(async () =>
            {
                await using var uof = _uofFactory.Create();

                var sessionStates = await uof.GetSessionStates(events);

                _logger.LogInformation("Found {0} for events {EventTypes}\r\n{Events}", sessionStates.Select(s => s.Entity.SessionStateId), events, events.Select(ev => JsonSerializer.Serialize(ev)));

                if (sessionStates.Count == 0)
                    return new List<HandlingResult>();

                var handlers = sessionStates.Select(ss => HandleSessionStateEvents(events, ss));

                return await HandleExceptions(handlers);
            });
        }

        private async Task<List<HandlingResult>> HandleExceptions(IEnumerable<Task<HandlingResult>> handlers)
        {
            var results = await Task.WhenAll(handlers.Select(r => r.ResultOrException()));

            var exceptions = results.Where(r => r.Exception != null).Select(r => r.Exception!).ToList();

            if (exceptions.Count > 0)
            {
                var concurrencyException = exceptions.OfType<ConcurrencyException>().FirstOrDefault();

                if (concurrencyException != null)
                    throw concurrencyException;

                if (exceptions.Count == 1)
                    throw exceptions.Single();

                throw new AggregateException("Multiple tasks failed", exceptions);
            }

            return results.Select(r => r.Result!).ToList();
        }

        private Task<ISessionStateMemento> CreateNewSessionState(string workflowId, ISessionStateUnitOfWork uof, TContext context)
        {
            var sessionState = new SessionStateEntity
            {
                SessionStateId = Guid.NewGuid(),
                WorkflowId = workflowId,
                Status = SessionStateStatus.InProgress,
                Steps = new List<SessionStepEntity>(),
                Items = new List<SessionItemEntity>(),
                Awaiters = new List<SessionEventAwaiterEntity>(),
                Counter = 0,
                PastEvents = new List<SessionEventEntity>(),
                Context = context
            };

            return uof.Add(sessionState);
        }

        private async Task<HandlingResult> HandleSessionStateEvent<TEvent>(TEvent @event, ISessionStateMemento sessionStateMemento)
            where TEvent : class
        {
            var sessionStateEntity = sessionStateMemento.Entity;
            var sessionState = ToSessionState(sessionStateEntity);

            CheckIgnoreVersion(@event, sessionState);

            bool isAdded = StateMachine.AddEvent(sessionState, @event, _eventAwaiterResolver.GetEventAwaiters(@event));
            if (!isAdded)
                return HandlingResult.Ignored(sessionStateEntity.SessionStateId, sessionState.Context);

            return await HandleSessionState(sessionState, sessionStateMemento);
        }

        private async Task<HandlingResult> HandleSessionStateEvents(IEnumerable<object> events, ISessionStateMemento sessionStateMemento)
        {
            SessionStateEntity sessionStateEntity = sessionStateMemento.Entity;
            var sessionState = ToSessionState(sessionStateEntity);

            CheckIgnoreVersion(events, sessionState);

            bool isAdded = false;
            foreach (var @event in events)
                isAdded = StateMachine.AddEvent(sessionState, @event, _eventAwaiterResolver.GetEventAwaiters(@event)) || isAdded;

            if (!isAdded)
                return HandlingResult.Ignored(sessionStateEntity.SessionStateId, sessionState.Context);

            return await HandleSessionState(sessionState, sessionStateMemento);
        }

        private void CheckIgnoreVersion(object @event, SessionState sessionState)
        {
            var ignoreSessionVersion = _eventAwaiterResolver.GetSessionVersionIgnore(@event);
            if (ignoreSessionVersion != null
                && ignoreSessionVersion.SessionId == sessionState.SessionStateId
                && ignoreSessionVersion.Version == sessionState.Version
            )
                throw new ConcurrencyException($"Version {ignoreSessionVersion.Version} of session {ignoreSessionVersion.SessionId} is not finished");
        }

        private void CheckIgnoreVersion(IEnumerable<object> events, SessionState sessionState)
        {
            foreach (var @event in events)
                CheckIgnoreVersion(@event, sessionState);
        }

        private async Task<HandlingResult> HandleSessionState(SessionState sessionState, ISessionStateMemento sessionStateMemento)
        {
            return await HandleSessionState(sessionState, await _workflowResolver.GetWorkflow(sessionState.WorkflowId), sessionStateMemento);
        }

        private async Task<HandlingResult> HandleSessionState(SessionState sessionState, IWorkflow workflow, ISessionStateMemento sessionStateMemento)
        {
            var storage = new SessionStateStorage(PersistStrategy.Default, st =>
            {
                UpdateSessionStateEntity(st, sessionStateMemento.Entity);
                _logger.LogInformation($"Saving changes for {sessionState.SessionStateId}");
                return sessionStateMemento.Save();
            });

            return await StateMachine.HandleWorkflow(sessionState, storage, workflow);
        }

        private async Task<HandlingResult> StartHandleSessionState<TSource>(TSource source, SessionStateEntity sessionStateEntity, SessionState sessionState, IWorkflow<TSource> workflow, ISessionStateMemento sessionStateMemento)
        {
            var storage = new SessionStateStorage(PersistStrategy.Default, st =>
            {
                UpdateSessionStateEntity(st, sessionStateEntity);
                _logger.LogInformation($"Saving changes for {sessionState.SessionStateId}");
                return sessionStateMemento.Save();
            });

            return await StateMachine.StartHandleWorkflow(source, sessionState, storage, workflow);
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
                Event = e.Event,
                EventType = e.EventType,
                SequenceNumber = e.SequenceNumber,
                Awaiters = e.Awaiters,
                Handled = e.Handled
            }).ToList();
        }
    }
}
