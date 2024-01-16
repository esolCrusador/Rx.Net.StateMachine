using Microsoft.Extensions.Logging;
using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Flow;
using Rx.Net.StateMachine.Helpers;
using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Storage;
using Rx.Net.StateMachine.WorkflowFactories;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine
{
    public class StateMachine
    {
        public JsonSerializerOptions SerializerOptions { get; }

        public StateMachine() : this(new JsonSerializerOptions())
        {
        }

        public StateMachine(JsonSerializerOptions serializerOptions)
        {
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

        public Task<HandlingResult> StartHandleWorkflow(object context, IWorkflow workflowFactory)
        {
            var sessionState = new SessionState(workflowFactory.WorkflowId, context);

            return HandleWorkflow(sessionState, workflowFactory);
        }

        public Task<HandlingResult> HandleWorkflow(SessionState sessionState, IWorkflow workflowFactory)
        {
            return HandleWorkflow(sessionState, SessionStateStorage.Empty, workflowFactory);
        }

        public Task<HandlingResult> HandleWorkflow(SessionState sessionState, ISessionStateStorage storage, IWorkflow workflowFactory)
        {
            var workflow = workflowFactory.Execute(new StateMachineScope(this, sessionState, storage).StartFlow());

            return HandleWorkflowResult(workflow, sessionState, storage);
        }

        public Task<HandlingResult> StartHandleWorkflow<TSource>(TSource source, object context, IWorkflow<TSource> workflowFactory)
        {
            return StartHandleWorkflow(source, context, SessionStateStorage.Empty, workflowFactory);
        }

        public Task<HandlingResult> StartHandleWorkflow<TSource>(TSource source, object context, ISessionStateStorage storage, IWorkflow<TSource> workflowFactory)
        {
            var sessionState = new SessionState(workflowFactory.WorkflowId, context);
            var workflow = workflowFactory.Execute(new StateMachineScope(this, sessionState, storage).StartFlow(source));

            return HandleWorkflowResult(workflow, sessionState, storage);
        }

        public Task<HandlingResult> StartHandleWorkflow<TSource>(TSource source, SessionState sessionState, ISessionStateStorage storage, IWorkflow<TSource> workflowFactory)
        {
            var workflow = workflowFactory.Execute(new StateMachineScope(this, sessionState, storage).StartFlow(source));

            return HandleWorkflowResult(workflow, sessionState, storage);
        }

        public SessionState ParseSessionState(object context, string stateString)
        {
            using var stateStream = CompressionHelper.Unzip(stateString);
            var minimalSessionState = JsonSerializer.Deserialize<MinimalSessionState>(stateStream, SerializerOptions)!;

            return new SessionState(
                null,
                minimalSessionState.WorkflowId,
                context,
                false,
                minimalSessionState.Counter,
                minimalSessionState.Steps ?? new Dictionary<string, SessionStateStep>(),
                minimalSessionState.Items ?? new Dictionary<string, object?>(),
                new List<PastSessionEvent>(),
                new List<SessionEventAwaiter>()
            );
        }

        private async Task<HandlingResult> HandleWorkflowResult<TResult>(IFlow<TResult> workflow, SessionState sessionState, ISessionStateStorage storage)
        {
            bool isFinished = default;
            int initialStepsCount = sessionState.Counter;

            isFinished = await workflow.Observable.Select(result => true)
                .DefaultIfEmpty(false)
                .ToTask();

            if (isFinished == false)
                sessionState.Status = SessionStateStatus.InProgress;
            else
            {
                sessionState.Status = SessionStateStatus.Completed;
                sessionState.Result = "Finished";
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
                sessionState.Context
            );
        }
    }
}
