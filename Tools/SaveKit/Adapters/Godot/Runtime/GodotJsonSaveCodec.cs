#if GODOT
using System;
using System.Reflection;
using System.Text.Json;

namespace YokiFrame.Godot
{
    /// <summary>
    /// Godot/.NET System.Text.Json 到 SaveKit JSON 契约的适配器。
    /// </summary>
    public sealed class GodotJsonSaveCodec : IJsonSaveCodec
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        /// <inheritdoc />
        public string Serialize<T>(T data)
        {
            return JsonSerializer.Serialize(data, Options);
        }

        /// <inheritdoc />
        public T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        /// <inheritdoc />
        public string Serialize(object data)
        {
            return JsonSerializer.Serialize(data, data.GetType(), Options);
        }

        /// <inheritdoc />
        public void DeserializeOverwrite(string json, object target)
        {
            var restored = JsonSerializer.Deserialize(json, target.GetType(), Options);
            CopyInstanceValues(restored, target, target.GetType());
        }

        /// <summary>把新建对象的字段和可写属性复制到现有对象。</summary>
        private static void CopyInstanceValues(object source, object target, Type targetType)
        {
            if (source == null)
            {
                throw new InvalidOperationException("JSON payload produced a null object.");
            }

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var fields = targetType.GetFields(flags);
            for (var i = 0; i < fields.Length; i++)
            {
                fields[i].SetValue(target, fields[i].GetValue(source));
            }

            var properties = targetType.GetProperties(flags);
            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    property.SetValue(target, property.GetValue(source, null), null);
                }
            }
        }
    }
}
#endif
