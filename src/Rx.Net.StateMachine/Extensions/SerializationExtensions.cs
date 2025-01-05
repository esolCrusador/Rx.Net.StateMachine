using System;
using System.Text.Json;

namespace Rx.Net.StateMachine.Extensions
{
    public static class SerializationExtensions
    {
        public static TValue? DeserializeValue<TValue>(this object? value, JsonSerializerOptions options, Func<JsonElement, JsonSerializerOptions, TValue>? deserializeOldValue)
        {
            if (value == default)
                return default;

            if (value is TValue v)
                return v;

            if (value is JsonElement jsonElement)
                try
                {
                    return jsonElement.Deserialize<TValue>(options);
                }
                catch (JsonException) when (deserializeOldValue != null)
                {
                    return deserializeOldValue(jsonElement, options);
                }

            throw new NotSupportedException($"Value type {value.GetType().FullName} is not {typeof(TValue).FullName}");
        }
    }
}
