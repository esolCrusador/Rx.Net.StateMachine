using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace Rx.Net.StateMachine.Persistance.Extensions
{
    public static class ExceptionExtensions
    {
        [DoesNotReturn] public static void Rethrow(this Exception ex) => ExceptionDispatchInfo.Capture(ex).Throw();
    }
}
