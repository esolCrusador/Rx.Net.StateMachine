using Microsoft.Extensions.Logging;
using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Exceptions;
using Rx.Net.StateMachine.Flow;
using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Storage;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine
{
    public class StateMachine
    {
        private readonly WorkflowFatalExceptions _workflowFatalExceptions;
        private readonly ILogger _logger;
        public JsonSerializerOptions SerializerOptions { get; }

        public StateMachine(WorkflowFatalExceptions workflowFatalExceptions, ILogger<StateMachine> logger)
            : this(workflowFatalExceptions, logger, new JsonSerializerOptions())
        {
        }

        public StateMachine(WorkflowFatalExceptions workflowFatalExceptions, ILogger<StateMachine> logger, JsonSerializerOptions serializerOptions)
        {
            _workflowFatalExceptions = workflowFatalExceptions;
            _logger = logger;
            SerializerOptions = serializerOptions;
        }

        public bool AddEvent<TEvent>(SessionState sessionState, TEvent @event, IEnumerable<IEventAwaiter<TEvent>> eventAwaiters)
            where TEvent : class
        {
            return sessionState.AddEvent(@event, eventAwaiters);
        }

        public bool AddEvent(SessionState sessionState, object @event, IEnumerable<IEventAwaiter> eventAwaiters)
        {
            return sessionState.AddEvent(@event, eventAwaiters);
        }

        public void ForceAddEvent<TEvent>(SessionState sessionState, TEvent @event)
            where TEvent : class
        {
            sessionState.ForceAddEvent(@event);
        }

        public Task<HandlingResult> HandleWorkflow(SessionState sessionState, ISessionStateStorage storage, IWorkflow workflowFactory, CancellationToken cancellationToken)
        {
            var workflow = workflowFactory.Execute(new StateMachineScope(this, sessionState, storage, cancellationToken).StartFlow());

            return HandleWorkflowResult(workflow, sessionState, storage);
        }

        public Task<HandlingResult> StartHandleWorkflow(object context, IItems? items, ISessionStateStorage storage, IWorkflow workflowFactory, CancellationToken cancellationToken)
        {
            var sessionState = new SessionState(workflowFactory.WorkflowId, context, items);
            var workflow = workflowFactory.Execute(new StateMachineScope(this, sessionState, storage, cancellationToken).StartFlow());

            return HandleWorkflowResult(workflow, sessionState, storage);
        }

        public Task<HandlingResult> StartHandleWorkflow<TSource>(TSource source, object context, IItems? items, ISessionStateStorage storage, IWorkflow<TSource> workflowFactory, CancellationToken cancellationToken)
        {
            var sessionState = new SessionState(workflowFactory.WorkflowId, context, items);
            var workflow = workflowFactory.Execute(new StateMachineScope(this, sessionState, storage, cancellationToken).StartFlow(source));

            return HandleWorkflowResult(workflow, sessionState, storage);
        }

        public Task<HandlingResult> StartHandleWorkflow<TSource>(TSource source, SessionState sessionState, ISessionStateStorage storage, IWorkflow<TSource> workflowFactory, CancellationToken cancellationToken)
        {
            var workflow = workflowFactory.Execute(new StateMachineScope(this, sessionState, storage, cancellationToken).StartFlow(source));

            return HandleWorkflowResult(workflow, sessionState, storage);
        }

        public SessionState ParseSessionState(object context, string stateString)
        {
            var minimalSessionState = MinimalSessionState.Parse(stateString, SerializerOptions)!;

            return new SessionState(
                null,
                minimalSessionState.WorkflowId,
                context,
                false,
                minimalSessionState.Steps?.Count ?? 0,
                minimalSessionState.Steps?.Select((kvp, idx) => new { kvp.Key, kvp.Value, SequenceNumber = idx + 1 })
                    .ToDictionary(v => v.Key, v => new SessionStateStep(v.Value, v.SequenceNumber)) ?? [],
                minimalSessionState.Items ?? [],
                new List<PastSessionEvent>(),
                new List<SessionEventAwaiter>()
            );
        }

        private async Task<HandlingResult> HandleWorkflowResult(IFlow<string?> workflow, SessionState sessionState, ISessionStateStorage storage)
        {
            bool isFinished = default;
            int initialStepsCount = sessionState.Counter;

            Exception? exception = null;
            try
            {
                var result = await workflow.Observable.Select(result => new ExecutionResult { IsFinished = true, Result = result })
                    .DefaultIfEmpty(new ExecutionResult { IsFinished = false })
                    .ToTask(workflow.Scope.CancellationToken);
                isFinished = result.IsFinished;


                if (isFinished == false)
                    sessionState.Status = SessionStateStatus.InProgress;
                else
                {
                    sessionState.Status = SessionStateStatus.Completed;
                    sessionState.Result = result.Result;
                }
            }
            catch (Exception ex) when (_workflowFatalExceptions.IsFatal(ex))
            {
                sessionState.Status = SessionStateStatus.Failed;
                exception = ex;
                sessionState.Result = ex.ToString();
                _logger.LogError(ex, "Session {SessionId} {WorkflowId} failed", sessionState.SessionStateId, sessionState.WorkflowId);
            }


            sessionState.RemoveNotHandledEvents(SerializerOptions);

            await storage.PersistSessionState(sessionState);

            var status = sessionState.Status == SessionStateStatus.Failed
                ? HandlingStatus.Failed
                    : isFinished
                        ? HandlingStatus.Finished
                        : HandlingStatus.Handled;

            return new HandlingResult(
                sessionState.SessionStateId,
                status,
                sessionState.Counter - initialStepsCount,
                sessionState.Result,
                exception
            );
        }
    }
}
