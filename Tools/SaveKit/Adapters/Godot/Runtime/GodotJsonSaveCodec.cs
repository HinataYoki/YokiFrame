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
        private static readonly JsonSerializerOptions sOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        /// <inheritdoc />
        public string Serialize<T>(T data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return JsonSerializer.Serialize(data, sOptions);
        }

        /// <inheritdoc />
        public T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("JSON payload cannot be empty.", nameof(json));
            return JsonSerializer.Deserialize<T>(json, sOptions);
        }

        /// <inheritdoc />
        public string Serialize(object data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return JsonSerializer.Serialize(data, data.GetType(), sOptions);
        }

        /// <inheritdoc />
        public void DeserializeOverwrite(string json, object target)
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new ArgumentException("JSON payload cannot be empty.", nameof(json));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Type targetType = target.GetType();
            object restored = JsonSerializer.Deserialize(json, targetType, sOptions);
            if (restored == null)
            {
                throw new InvalidOperationException("JSON payload produced a null object.");
            }

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                CopyPresentMembers(restored, target, targetType, document.RootElement);
            }
        }

        /// <summary>只回拷 JSON 中真实出现的公开字段和可写属性，未出现的成员保持原值。</summary>
        private static void CopyPresentMembers(object source, object target, Type targetType, JsonElement root)
        {
            const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.Instance;
            FieldInfo[] fields = targetType.GetFields(FLAGS);
            for (var i = 0; i < fields.Length; i++)
            {
                if (HasMember(root, fields[i].Name)) fields[i].SetValue(target, fields[i].GetValue(source));
            }

            PropertyInfo[] properties = targetType.GetProperties(FLAGS);
            for (var i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0 && HasMember(root, property.Name))
                {
                    property.SetValue(target, property.GetValue(source, null), null);
                }
            }
        }

        /// <summary>按 PropertyNameCaseInsensitive 对齐的规则判断 JSON 是否包含该成员。</summary>
        private static bool HasMember(JsonElement root, string name)
        {
            if (root.ValueKind != JsonValueKind.Object) return false;
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
#endif
