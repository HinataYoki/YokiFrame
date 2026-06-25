#if GODOT
using System;
using System.Text.Json;
using YokiFrame;

namespace YokiFrame.Godot
{
    /// <summary>
    /// ISerializationProvider 的 Godot 实现。
    /// 为了兼容 Godot 4.7 的 C# Variant 泛型约束，这里直接走 BCL JsonSerializer。
    /// </summary>
    public sealed class GodotEngineSerializationProvider : ISerializationProvider
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public string Serialize<T>(T data)
        {
            return JsonSerializer.Serialize(data, SerializerOptions);
        }

        public T Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(json, SerializerOptions);
            }
            catch (JsonException)
            {
                return default;
            }
            catch (NotSupportedException)
            {
                return default;
            }
        }
    }
}
#endif
