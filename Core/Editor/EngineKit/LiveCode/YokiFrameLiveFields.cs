#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>Adapter-whitelisted fields only. Get/Set use FieldInfo, never user accessors.</summary>
    public sealed class YokiFrameLiveFieldBinding
    {
        private readonly object mInstance;
        private readonly FieldInfo mField;
        public Type ValueType => mField.FieldType;
        public YokiFrameLiveFieldBinding(object instance, FieldInfo field)
        {
            if (instance == null || field == null || field.IsStatic || field.IsInitOnly || field.IsLiteral)
                throw new ArgumentException("A mutable instance field is required.");
            mInstance = instance; mField = field;
        }
        public object Read() => mField.GetValue(mInstance);
        public void Write(object value) => mField.SetValue(mInstance, value);
    }

    public interface IYokiFrameLiveFieldHost
    {
        YokiFrameLiveFieldBinding BindTunableField(IDisposable attachment, string name);
        object DecodeTunableValue(Type type, JsonElement value);
    }

    public sealed class YokiFrameLiveFieldChange
    {
        public string Name { get; }
        public string TypeName { get; }
        public JsonElement Value { get; }
        internal YokiFrameLiveFieldChange(string name, string typeName, JsonElement value)
        {
            Name = name; TypeName = typeName; Value = value;
        }
    }

    public sealed class YokiFrameLiveFieldUpdate
    {
        public string Id { get; internal set; }
        public int Revision { get; internal set; }
        internal readonly List<YokiFrameLiveFieldChange> mFields = new List<YokiFrameLiveFieldChange>();
        public IReadOnlyList<YokiFrameLiveFieldChange> Fields => mFields.AsReadOnly();
    }

    public sealed class YokiFrameLiveFieldRequest
    {
        public const int MaxBytes = 48 * 1024;
        public const int MaxChanges = 256;
        public string SessionId { get; private set; }
        public long Generation { get; private set; }
        public string Target { get; private set; }
        internal readonly List<YokiFrameLiveFieldUpdate> mUpdates = new List<YokiFrameLiveFieldUpdate>();
        public IReadOnlyList<YokiFrameLiveFieldUpdate> Updates => mUpdates.AsReadOnly();

        public static YokiFrameLiveFieldRequest Parse(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxBytes)
                throw new ArgumentException("Field update payload exceeds 48 KiB.");
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 12 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("confirmed", out var confirmed)
                || confirmed.ValueKind != JsonValueKind.True)
                throw new ArgumentException("Field updates require confirmed:true.");
            var request = new YokiFrameLiveFieldRequest
            {
                SessionId = RequiredText(root, "sessionId"), Target = RequiredText(root, "target")
            };
            if (!root.GetProperty("generation").TryGetInt64(out long generation) || generation <= 0)
                throw new ArgumentException("A positive generation is required.");
            request.Generation = generation;
            var updates = root.GetProperty("updates");
            if (updates.GetArrayLength() < 1 || updates.GetArrayLength() > 64)
                throw new ArgumentException("updates must contain 1..64 behaviours.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            foreach (var update in updates.EnumerateArray())
            {
                var item = new YokiFrameLiveFieldUpdate { Id = RequiredText(update, "id") };
                YokiFrameLiveCodeManager.ValidateName(item.Id);
                if (!ids.Add(item.Id)) throw new ArgumentException("Duplicate update ID.");
                if (!update.GetProperty("revision").TryGetInt32(out int revision) || revision <= 0)
                    throw new ArgumentException("A positive behaviour revision is required.");
                item.Revision = revision;
                var fields = update.GetProperty("fields");
                if (fields.GetArrayLength() < 1 || fields.GetArrayLength() > MaxChanges)
                    throw new ArgumentException("fields must contain 1..256 entries.");
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var field in fields.EnumerateArray())
                {
                    if (++count > MaxChanges) throw new ArgumentException("Field batch limit is 256.");
                    string name = RequiredText(field, "name");
                    if (!names.Add(name)) throw new ArgumentException("Duplicate field name.");
                    item.mFields.Add(new YokiFrameLiveFieldChange(name, RequiredText(field, "type"), field.GetProperty("value")));
                }
                request.mUpdates.Add(item);
            }
            return request;
        }

        internal static string RequiredText(JsonElement json, string name)
        {
            string value = json.GetProperty(name).GetString();
            if (string.IsNullOrEmpty(value) || value.Length > 256)
                throw new ArgumentException(name + " must be a nonempty string up to 256 characters.");
            return value;
        }
    }

    public static class YokiFrameLiveFieldValues
    {
        public static bool TryDecodeScalar(Type type, JsonElement json, out object value)
        {
            value = null;
            if (type == typeof(string))
            {
                if (json.ValueKind != JsonValueKind.String && json.ValueKind != JsonValueKind.Null)
                    throw new ArgumentException("String field requires a JSON string or null.");
                value = json.GetString();
                return true;
            }
            if (type == typeof(int))
            {
                if (!json.TryGetInt32(out int number)) throw new ArgumentException("Int32 field requires an integer in range.");
                value = number;
                return true;
            }
            if (type == typeof(float))
            {
                value = ReadSingle(json);
                return true;
            }
            if (type == typeof(bool))
            {
                value = json.GetBoolean();
                return true;
            }
            if (type.IsEnum)
            {
                string name = json.GetString();
                if (name == null || !Enum.IsDefined(type, name))
                    throw new ArgumentException("Enum field requires an exact declared name.");
                value = Enum.Parse(type, name);
                return true;
            }
            return false;
        }

        public static float ReadSingle(JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Number || !float.TryParse(json.ToString(),
                NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || float.IsNaN(number) || float.IsInfinity(number))
                throw new ArgumentException("Single field requires a finite JSON number in range.");
            return number;
        }
    }
}
#endif
