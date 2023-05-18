using System;

namespace Rx.Net.StateMachine.Tests.Controls
{
    public interface IControl
    {
    }

    public interface IControl<TSource, TResult>: IControl
    {
        IObservable<TResult> StartDialog(StateMachineScope scope, TSource source);
    }
}
