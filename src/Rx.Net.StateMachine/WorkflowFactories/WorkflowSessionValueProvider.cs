using System;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public class WorkflowSessionValueProvider<TValue>
        where TValue : class
    {
        private TValue? _value;
        public TValue Value => _value ?? throw new InvalidOperationException($"Value {typeof(TValue).FullName} was not provided");

        public void InitValue(TValue value) => _value = value;
    }
}
