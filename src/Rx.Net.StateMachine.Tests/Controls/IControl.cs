using Rx.Net.StateMachine.Flow;
using System;

namespace Rx.Net.StateMachine.Tests.Controls
{
    public interface IControl
    {
    }

    public interface IControl<TSource, TResult>: IControl
    {
        IFlow<TResult> StartDialog(StateMachineScope scope, TSource source);
    }
}
