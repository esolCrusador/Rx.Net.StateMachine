using System;
using System.Collections.Generic;
using System.Linq;

namespace Rx.Net.StateMachine.Exceptions
{
    public class WorkflowFatalExceptions
    {
        private Dictionary<Type, Func<Exception, bool>?> _fatals;

        public WorkflowFatalExceptions(IEnumerable<WorkflowFatalExceptionRegistration> workflowFatalExceptionRegistration)
        {
            _fatals = workflowFatalExceptionRegistration.ToDictionary(f => f.ExceptionType, f => f.Filter);
        }

        public bool IsFatal(Exception ex)
        {
            if (!_fatals.TryGetValue(ex.GetType(), out var filter))
                return false;

            return filter == null || filter(ex);
        }
    }

    public class WorkflowFatalExceptionRegistration
    {
        public readonly Type ExceptionType;
        public readonly Func<Exception, bool>? Filter;

        public WorkflowFatalExceptionRegistration(Type exceptionType, Func<Exception, bool>? filter)
        {
            ExceptionType = exceptionType;
            Filter = filter;
        }
    }
}
