using System;

namespace Rx.Net.StateMachine.Extensions
{
    public static class ValueExtensions
    {
        public static TValue GetValue<TValue>(this TValue? value, string argumentName)
            where TValue : class => value ?? throw new ArgumentNullException(argumentName, "Argument must be not null");

        public static TValue GetValue<TValue>(this TValue? value, string argumentName)
            where TValue : struct => value ?? throw new ArgumentNullException(argumentName, "Argument must be not null");
    }
}
