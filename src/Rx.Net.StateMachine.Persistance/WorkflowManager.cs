using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
using System.Threading;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Persistance
{
    public class WorkflowManager<TContext>
        where TContext : class
    {
        private static readonly HandlingResult[] EmptyHandlingResult = [];
        private readonly AsyncPolicy _concurrencyRetry;
        private readonly ILogger<WorkflowManager<TContext>> _logger;
        private readonly ISessionStateUnitOfWorkFactory _uofFactory;
        private readonly IWorkflowResolver _workflowResolver;
        private readonly IEventAwaiterResolver _eventAwaiterResolver;
        private readonly IOptions<StateMachineConfiguration> _configuration;
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public StateMachine StateMachine { get; }

        public WorkflowManager(ILogger<WorkflowManager<TContext>> logger, ISessionStateUnitOfWorkFactory uofFactory, IWorkflowResolver workflowResolver, IEventAwaiterResolver eventAwaiterResolver, StateMachine stateMachine, IOptions<StateMachineConfiguration> configuration, JsonSerializerOptions jsonSerializerOptions)
        {
            _logger = logger;
            _uofFactory = uofFactory;
            _workflowResolver = workflowResolver;
            _eventAwaiterResolver = eventAwaiterResolver;
            StateMachine = stateMachine;
            _configuration = configuration;
            _jsonSerializerOptions = jsonSerializerOptions;
            _concurrencyRetry = Policy.Handle<ConcurrencyException>().RetryForeverAsync(ex => _logger.LogWarning(ex.Message));
        }

        public WorkflowManager(ILogger<WorkflowManager<TContext>> logger, ISessionStateUnitOfWorkFactory uofFactory, IWorkflowResolver workflowResolver, IEventAwaiterResolver eventAwaiterResolver, StateMachine stateMachine, IOptions<StateMachineConfiguration> configuration)
            : this(logger, uofFactory, workflowResolver, eventAwaiterResolver, stateMachine, configuration, new JsonSerializerOptions())
        {
        }

        public struct WorkflowRunner
        {
            private readonly WorkflowManager<TContext> _workflowManager;
            private readonly IWorkflowResolver _workflowResolver;
            private readonly TContext _context;
            private BeforePersistScope? _beforePersistScope;

            public WorkflowRunner(WorkflowManager<TContext> workflowManager, IWorkflowResolver workflowResolver, TContext context)
            {
                _workflowManager = workflowManager;
                _workflowResolver = workflowResolver;
                _context = context;
            }
            public WorkflowRunner BeforePersist(BeforePersistScope beforePersistScope)
            {
                _beforePersistScope = beforePersistScope;
                return this;
            }
            public async Task<HandlingResult> Workflow<TWorkflow>() where TWorkflow : class, IWorkflow
            {
                await using var workflowSession = _workflowResolver.GetWorkflowSession<TWorkflow>(_beforePersistScope, _context);
                return await _workflowManager.StartHandle(workflowSession, _context);
            }

            public async Task<HandlingResult> Workflow(string workflowId)
            {
                await using var workflowSession = _workflowResolver.GetWorkflowSession(workflowId, _beforePersistScope, _context);
                return await _workflowManager.StartHandle(workflowSession, _context);
            }
        }

        public WorkflowRunner Start(TContext context) =>
            new WorkflowRunner(this, _workflowResolver, context);

        private async Task<HandlingResult> StartHandle(WorkflowSession workflowSession, TContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            await using var uof = _uofFactory.Create();
            var sessionStateMemento = CreateNewSessionState(workflowSession.Workflow.WorkflowId, uof, context);
            var sessionState = ToSessionState(sessionStateMemento.Entity);

            return await HandleSessionState(sessionState, workflowSession, sessionStateMemento);
        }

        public struct WorkflowRunner<TSource>
        {
            private readonly WorkflowManager<TContext> _workflowManager;
            private readonly IWorkflowResolver _workflowResolver;
            private readonly TContext _context;
            private readonly TSource _source;
            private BeforePersistScope? _beforePersistScope;

            public WorkflowRunner(WorkflowManager<TContext> workflowManager, IWorkflowResolver workflowResolver, TContext context, TSource source)
            {
                _workflowManager = workflowManager;
                _workflowResolver = workflowResolver;
                _context = context;
                _source = source;
            }

            public WorkflowRunner<TSource> BeforePersist(BeforePersistScope? beforePersistScope)
            {
                _beforePersistScope = beforePersistScope;
                return this;
            }

            public async Task<HandlingResult> Workflow<TWorkflow>() where TWorkflow : class, IWorkflow<TSource>
            {
                await using var workflowSession = _workflowResolver.GetWorkflowSession<TWorkflow>(_beforePersistScope, _context);
                return await _workflowManager.StartHandle(_source, workflowSession, _context);
            }

            public async Task<HandlingResult> Workflow(string workflowId)
            {
                await using var workflowSession = _workflowResolver.GetWorkflowSession(workflowId, _beforePersistScope, _context);
                return await _workflowManager.StartHandle(_source, workflowSession, _context);
            }
        }

        public WorkflowRunner<TSource> Start<TSource>(TContext context, TSource source) =>
            new WorkflowRunner<TSource>(this, _workflowResolver, context, source);

        private async Task<HandlingResult> StartHandle<TSource>(TSource source, WorkflowSession workflowSession, TContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            await using var uof = _uofFactory.Create();
            var sessionStateMemento = CreateNewSessionState(workflowSession.Workflow.WorkflowId, uof, context);
            var sessionState = ToSessionState(sessionStateMemento.Entity);

            return await StartHandleSessionState<TSource>(source, sessionStateMemento.Entity, sessionState, workflowSession, sessionStateMemento);
        }

        public async Task RemoveDefaultSesssions(Guid? newDefaultSessionId, string userContextId, CancellationToken cancellationToken)
        {
            await HandleEvent(new DefaultSessionRemoved { SessionId = newDefaultSessionId, UserContextId = userContextId }, null, cancellationToken);
        }

        public async Task CancelSession(Guid sessionId, CancellationReason reason, CancellationToken cancellationToken)
        {
            Exception? exception = null;
            IReadOnlyList<HandlingResult>? result = null;
            try
            {
                await HandleEvent(new BeforeSessionCancelled(sessionId, reason), null, cancellationToken);

                result = await HandleEvent(new SessionCancelled(sessionId, reason), null, cancellationToken);
                if (result.Count > 1)
                    throw new InvalidOperationException($"Invalid session {sessionId} handled {result.Count} times");

                if (result.Count == 1)
                {
                    var status = result[0].Status;
                    if (status == HandlingStatus.Finished)
                        return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to cancell session {sessionId}");
                exception = ex;
            }

            await using var uof = _uofFactory.Create();
            var session = await uof.GetSessionState(sessionId, cancellationToken);
            if (session == null)
            {
                _logger.LogWarning($"Could not find session {sessionId}");
                return;
            }

            if (session.Entity.Status == SessionStateStatus.Completed)
                return;

            if (exception == null)
            {
                session.Entity.Status = SessionStateStatus.Cancelled;
                session.Entity.Result = $"Cancelled because {JsonSerializer.Serialize(result, _jsonSerializerOptions)}";
                _logger.LogWarning($"Could not finish session {sessionId}. Cancelling...");
            }
            else
            {
                session.Entity.Status = SessionStateStatus.Failed;
                session.Entity.Result = exception.ToString();
            }

            await session.Save();
        }

        public async Task<SessionStateStatus?> GetStatus(Guid sessionId, CancellationToken cancellationToken)
        {
            await using var ouf = _uofFactory.Create();
            var memento = await ouf.GetSessionState(sessionId, cancellationToken);

            return memento?.Entity.Status;
        }

        public Task<IReadOnlyList<HandlingResult>> HandleEvent<TEvent>(TEvent @event, BeforePersistScope? beforePersist, CancellationToken cancellationToken)
            where TEvent : class
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));

            return _concurrencyRetry.ExecuteAsync(async cancellation =>
            {
                await using var uof = _uofFactory.Create();

                var sessionStates = await uof.GetSessionStates(@event, cancellationToken);

                _logger.LogInformation("Found {SessionIds} for event {EventType}\r\n{Event}", sessionStates.Select(s => s.Entity.SessionStateId), @event.GetType().FullName, JsonSerializer.Serialize(@event, _jsonSerializerOptions));

                if (sessionStates.Count == 0)
                    return new List<HandlingResult>();
                return await HandleSessionStates(sessionStates, (sessionState, ccl) => HandleSessionStateEvent(@event, sessionState, beforePersist), cancellation);
            }, cancellationToken);
        }

        public Task<IReadOnlyList<HandlingResult>> HandleEvents(IReadOnlyCollection<object> events, BeforePersistScope? beforePersist, CancellationToken cancellationToken)
        {
            if (events.Count == 0)
                throw new ArgumentException(nameof(events));

            return _concurrencyRetry.ExecuteAsync(async cancellation =>
            {
                await using var uof = _uofFactory.Create();

                var sessionStates = await uof.GetSessionStates(events, cancellationToken);

                _logger.LogInformation("Found {0} for events {EventTypes}\r\n{Events}",
                    sessionStates.Select(s => s.Entity.SessionStateId),
                    string.Join(", ", events.Select(ev => ev.GetType().FullName)),
                    JsonSerializer.Serialize(events, _jsonSerializerOptions)
                );

                if (sessionStates.Count == 0)
                    return new List<HandlingResult>();

                return await HandleSessionStates(sessionStates, (sessionState, ccl) => HandleSessionStateEvents(events, sessionState, beforePersist), cancellation);
            }, cancellationToken);
        }

        private async Task<IReadOnlyList<HandlingResult>> HandleSessionStates(
            IReadOnlyCollection<ISessionStateMemento> sessionStates,
            Func<ISessionStateMemento, CancellationToken, Task<HandlingResult>> handle,
            CancellationToken cancellationToken
        )
        {
            if (sessionStates.Count == 0)
                return EmptyHandlingResult;

            List<HandlingResult> results = new List<HandlingResult>(sessionStates.Count);

            await Parallel.ForEachAsync(sessionStates.Select((h, idx) => new KeyValuePair<int, ISessionStateMemento>(idx, h)), new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _configuration.Value.EventHandlingParallelism
            }, async (kvp, cancellation) =>
            {
                results[kvp.Key] = await handle(kvp.Value, cancellation);
            });

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

            return results.Select(r => r!).ToList();
        }

        private ISessionStateMemento CreateNewSessionState(string workflowId, ISessionStateUnitOfWork uof, TContext context)
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

        private async Task<HandlingResult> HandleSessionStateEvent<TEvent>(TEvent @event, ISessionStateMemento sessionStateMemento, BeforePersistScope? beforePersist)
            where TEvent : class
        {
            var sessionStateEntity = sessionStateMemento.Entity;
            var sessionState = ToSessionState(sessionStateEntity);

            CheckStaleVersion(@event, sessionState);

            bool isAdded = StateMachine.AddEvent(sessionState, @event, _eventAwaiterResolver.GetEventAwaiters(@event));
            if (!isAdded)
                return HandlingResult.Ignored(sessionStateEntity.SessionStateId, sessionState.Context);

            return await HandleSessionState(sessionState, sessionStateMemento, beforePersist);
        }

        private async Task<HandlingResult> HandleSessionStateEvents(IEnumerable<object> events, ISessionStateMemento sessionStateMemento, BeforePersistScope? beforePersist)
        {
            SessionStateEntity sessionStateEntity = sessionStateMemento.Entity;
            var sessionState = ToSessionState(sessionStateEntity);

            CheckIgnoreVersion(events, sessionState);

            bool isAdded = false;
            foreach (var @event in events)
                isAdded = StateMachine.AddEvent(sessionState, @event, _eventAwaiterResolver.GetEventAwaiters(@event)) || isAdded;

            if (!isAdded)
                return HandlingResult.Ignored(sessionStateEntity.SessionStateId, sessionState.Context);

            return await HandleSessionState(sessionState, sessionStateMemento, beforePersist);
        }

        private void CheckStaleVersion(object @event, SessionState sessionState)
        {
            var staleSessionVersion = _eventAwaiterResolver.GetStaleSessionVersion(@event);
            if (staleSessionVersion != null
                && staleSessionVersion.SessionId == sessionState.SessionStateId
                && staleSessionVersion.Version == sessionState.Version
            )
                throw new ConcurrencyException($"Version {staleSessionVersion.Version} of session {staleSessionVersion.SessionId} is not finished");
        }

        private void CheckIgnoreVersion(IEnumerable<object> events, SessionState sessionState)
        {
            foreach (var @event in events)
                CheckStaleVersion(@event, sessionState);
        }

        private async Task<HandlingResult> HandleSessionState(SessionState sessionState, ISessionStateMemento sessionStateMemento, BeforePersistScope? beforePersist)
        {
            await using var workflowSession = _workflowResolver.GetWorkflowSession(sessionState.WorkflowId, beforePersist, sessionState.Context);
            return await HandleSessionState(sessionState, workflowSession, sessionStateMemento);
        }

        private async Task<HandlingResult> HandleSessionState(SessionState sessionState, WorkflowSession workflowSession, ISessionStateMemento sessionStateMemento)
        {
            var storage = new SessionStateStorage(PersistStrategy.Default, async st =>
            {
                await workflowSession.BeforePersist(st);
                UpdateSessionStateEntity(st, sessionStateMemento.Entity);
                _logger.LogInformation($"Saving changes for {sessionState.SessionStateId}");
                await sessionStateMemento.Save();
            });

            return await StateMachine.HandleWorkflow(sessionState, storage, workflowSession.Workflow);
        }

        private async Task<HandlingResult> StartHandleSessionState<TSource>(TSource source, SessionStateEntity sessionStateEntity, SessionState sessionState, WorkflowSession workflowSession, ISessionStateMemento sessionStateMemento)
        {
            var storage = new SessionStateStorage(PersistStrategy.Default, async st =>
            {
                await workflowSession.BeforePersist(st);
                UpdateSessionStateEntity(st, sessionStateEntity);
                _logger.LogInformation($"Saving changes for {sessionState.SessionStateId}");
                await sessionStateMemento.Save();
            });

            return await StateMachine.StartHandleWorkflow(source, sessionState, storage, (IWorkflow<TSource>)workflowSession.Workflow);
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
