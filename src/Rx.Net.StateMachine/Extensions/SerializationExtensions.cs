using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Rx.Net.StateMachine.Extensions
{
    public static class SerializationExtensions
    {
        public static TValue? DeserializeValue<TValue>(this object? value, JsonSerializerOptions options)
        {
            if (value == default)
                return default;

            if (value is TValue v)
                return v;

            if (value is JsonElement jsonElement)
                return jsonElement.Deserialize<TValue>(options);

            throw new NotSupportedException($"Value type {value.GetType().FullName} is not {typeof(TValue).FullName}");
        }
    }
}
