using System;

namespace Rx.Net.StateMachine.WorkflowFactories
{
    public class ContextProvider
    {
        private object? _context;
        public object Context => _context ?? throw new InvalidOperationException("Context was not provided");

        public void SetContext(object context) => _context = context;
    }
}
