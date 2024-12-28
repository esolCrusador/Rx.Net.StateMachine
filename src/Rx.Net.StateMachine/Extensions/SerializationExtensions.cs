using System;
using System.Reactive;
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
                try
                {
                    return jsonElement.Deserialize<TValue>(options);
                }
                catch (JsonException) when (typeof(TValue) == typeof(string) && jsonElement.Deserialize<Unit>() == Unit.Default)
                {
                    return default(TValue); // TODO Remove after 01.01.2025
                }

            throw new NotSupportedException($"Value type {value.GetType().FullName} is not {typeof(TValue).FullName}");
        }
    }
}
