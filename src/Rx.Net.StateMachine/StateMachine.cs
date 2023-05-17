using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Helpers;
using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Storage;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
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
        {
            return sessionState.AddEvent(@event, eventAwaiters);
        }

        public void ForceAddEvent<TEvent>(SessionState sessionState, TEvent @event)
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
            var workflow = workflowFactory.Execute(new StateMachineScope(this, sessionState, storage));

            return HandleWorkflowResult(workflow, sessionState, storage);
        }

        public Task<HandlingResult> StartHandleWorkflow<TSource, TResult>(TSource source, object context, IWorkflow<TSource, TResult> workflowFactory)
        {
            return StartHandleWorkflow(source, context, SessionStateStorage.Empty, workflowFactory);
        }

        public Task<HandlingResult> StartHandleWorkflow<TSource, TResult>(TSource source, object context, ISessionStateStorage storage, IWorkflow<TSource, TResult> workflowFactory)
        {
            var sessionState = new SessionState(workflowFactory.WorkflowId, context);
            var workflow = workflowFactory.GetResult(StateMachineObservableExtensions.Of(source), new StateMachineScope(this, sessionState, storage));

            return HandleWorkflowResult(workflow, sessionState, storage);
        }

        public Task<HandlingResult> StartHandleWorkflow<TSource, TResult>(TSource source, SessionState sessionState, ISessionStateStorage storage, IWorkflow<TSource, TResult> workflowFactory)
        {
            var workflow = workflowFactory.GetResult(StateMachineObservableExtensions.Of(source), new StateMachineScope(this, sessionState, storage));

            return HandleWorkflowResult(workflow, sessionState, storage);
        }

        public SessionState ParseSessionState(object context, string stateString)
        {
            using var stateStream = CompressionHelper.Unzip(stateString);
            var minimalSessionState = JsonSerializer.Deserialize<MinimalSessionState>(stateStream, SerializerOptions);

            return new SessionState(
                null,
                minimalSessionState.WorkflowId,
                context,
                false,
                minimalSessionState.Counter,
                minimalSessionState.Steps ?? new Dictionary<string, SessionStateStep>(),
                minimalSessionState.Items ?? new Dictionary<string, string>(),
                new List<PastSessionEvent>(),
                new List<SessionEventAwaiter>()
            );
        }

        private async Task<HandlingResult> HandleWorkflowResult<TResult>(IObservable<TResult> workflow, SessionState sessionState, ISessionStateStorage storage)
        {
            bool isFinished = default;

            try
            {
                isFinished = await workflow.Select(result => true)
                    .DefaultIfEmpty(false)
                    .ToTask();

                if (isFinished == false)
                    sessionState.Status = SessionStateStatus.InProgress;
                else
                {
                    sessionState.Status = SessionStateStatus.Completed;
                    sessionState.SetResult(isFinished, SerializerOptions);
                }
            }
            catch (Exception ex)
            {
                sessionState.Status = SessionStateStatus.Failed;
                sessionState.Result = ex.ToString();
            }

            sessionState.RemoveNotHandledEvents(SerializerOptions);

            await storage.PersistSessionState(sessionState);

            return sessionState.Status == SessionStateStatus.Failed
                ? HandlingResult.Failed
                    : isFinished
                        ? HandlingResult.Finished
                        : HandlingResult.Handled;
        }

        private class WorkflowFinishResult<TResult>
        {
            public TResult Result { get; set; }
        }
    }
}
