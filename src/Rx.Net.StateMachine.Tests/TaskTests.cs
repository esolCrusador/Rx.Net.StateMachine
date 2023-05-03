using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Reactive;

namespace Rx.Net.StateMachine.Tests
{
    // https://www.figma.com/file/65UWsCMvohKGerVrUWWFdd/Task-Workflow?type=whiteboard&node-id=0-1&t=HtMQYV7kgZbTx2GO-0
    public abstract class TaskTests
    {
        class TaskWorkflowFactory : WorkflowFactory<int, Unit>
        {
            public TaskWorkflowFactory()
            {
            }
            public override string WorkflowId => "Task";

            public override IObservable<Unit> GetResult(IObservable<int> input, StateMachineScope scope)
            {
                throw new NotImplementedException();
            }
        }
    }
}
