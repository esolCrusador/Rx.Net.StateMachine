using System;
using System.Collections.Generic;
using System.Text;

namespace Rx.Net.StateMachine.Flow
{
    public interface IFlow
    {
        public StateMachineScope Scope { get; }
    }
    public interface IFlow<TElement>: IFlow
    {
        internal IObservable<TElement> Observable { get; }
    }
}
