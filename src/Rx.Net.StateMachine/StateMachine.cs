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

        public void AddEvent<TEvent>(SessionState sessionState, TEvent @event)
        {
            sessionState.AddEvent(@event);
        }

        public async Task<bool> HandleWorkflow<TResult>(SessionState sessionState, object context, ISessionStateStorage repository, Func<StateMachineScope, IObservable<TResult>> workflowFactory)
        {
            var workflow = workflowFactory(new StateMachineScope(this, context, sessionState, repository));
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

            return isFinished;
        }

        private class WorkflowFinishResult<TResult>
        {
            public TResult Result { get; set; }
        }
    }
}
