using Rx.Net.StateMachine.Helpers;
using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Storage;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine
{
    public class StateMachine
    {
        public JsonSerializerOptions SerializerOptions { get; set; }

        public bool AddEvent<TEvent>(SessionState sessionState, TEvent @event)
        {
            return sessionState.AddEvent(@event);
        }

        public void ForceAddEvent<TEvent>(SessionState sessionState, TEvent @event)
        {
            sessionState.ForceAddEvent(@event);
        }

        public Task<HandlingResult> StartHandleWorkflow<TResult>(object context, Func<StateMachineScope, IObservable<TResult>> workflowFactory)
        {
            var sessionState = new SessionState(context);

            return HandleWorkflow<TResult>(sessionState, workflowFactory);
        }

        public Task<HandlingResult> HandleWorkflow<TResult>(SessionState sessionState, Func<StateMachineScope, IObservable<TResult>> workflowFactory)
        {
            return HandleWorkflow<TResult>(sessionState, SessionStateStorage.Empty, workflowFactory);
        }

        public async Task<HandlingResult> HandleWorkflow<TResult>(SessionState sessionState, ISessionStateStorage repository, Func<StateMachineScope, IObservable<TResult>> workflowFactory)
        {
            var workflow = workflowFactory(new StateMachineScope(this, sessionState, repository));
            bool isFinished = default;

            try
            {
                isFinished = await workflow.Select(result => true).DefaultIfEmpty(false).ToTask();
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

            await repository.PersistSessionState(sessionState);

            return sessionState.Status == SessionStateStatus.Failed 
                ? HandlingResult.Failed 
                    : isFinished 
                        ? HandlingResult.Finished 
                        : HandlingResult.Handled;
        }

        public SessionState ParseSessionState(object context, string stateString)
        {
            using var stateStream = CompressionHelper.Unzip(stateString);
            var minimalSessionState = JsonSerializer.Deserialize<MinimalSessionState>(stateStream, SerializerOptions);

            return new SessionState(context, minimalSessionState.Counter, minimalSessionState.Steps, new List<PastSessionEvent>(), new List<SessionEventAwaiter>());
        }

        private class WorkflowFinishResult<TResult>
        {
            public TResult Result { get; set; }
        }
    }
}
