#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>Adapter-whitelisted fields only. Get/Set use FieldInfo, never user accessors.</summary>
    public sealed class LiveFieldBinding
    {
        private readonly object mInstance;
        private readonly FieldInfo mField;
        public Type ValueType => mField.FieldType;
        public LiveFieldBinding(object instance, FieldInfo field)
        {
            if (instance == null || field == null || field.IsStatic || field.IsInitOnly || field.IsLiteral)
                throw new ArgumentException("A mutable instance field is required.");
            mInstance = instance; mField = field;
        }
        public object Read() => mField.GetValue(mInstance);
        public void Write(object value) => mField.SetValue(mInstance, value);
    }

    public interface ILiveFieldHost
    {
        LiveFieldBinding BindTunableField(IDisposable attachment, string name);
        object DecodeTunableValue(Type type, JsonElement value);
    }

    public sealed class LiveFieldChange
    {
        public string Name { get; }
        public string TypeName { get; }
        public JsonElement Value { get; }
        internal LiveFieldChange(string name, string typeName, JsonElement value)
        {
            Name = name; TypeName = typeName; Value = value;
        }
    }

    public sealed class LiveFieldUpdate
    {
        public string Id { get; internal set; }
        public int Revision { get; internal set; }
        internal readonly List<LiveFieldChange> mFields = new List<LiveFieldChange>();
        public IReadOnlyList<LiveFieldChange> Fields => mFields.AsReadOnly();
    }

    public sealed class LiveFieldRequest
    {
        public const int MaxBytes = 48 * 1024;
        public const int MaxChanges = 256;
        public string SessionId { get; private set; }
        public long Generation { get; private set; }
        public string Target { get; private set; }
        internal readonly List<LiveFieldUpdate> mUpdates = new List<LiveFieldUpdate>();
        public IReadOnlyList<LiveFieldUpdate> Updates => mUpdates.AsReadOnly();

        public static LiveFieldRequest Parse(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxBytes)
                throw new ArgumentException("Field update payload exceeds 48 KiB.");
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = LiveFieldValues.MaxDepth + 7 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("confirmed", out var confirmed)
                || confirmed.ValueKind != JsonValueKind.True)
                throw new ArgumentException("Field updates require confirmed:true.");
            var request = new LiveFieldRequest
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
                var item = new LiveFieldUpdate { Id = RequiredText(update, "id") };
                LiveCodeManager.ValidateName(item.Id);
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
                    item.mFields.Add(new LiveFieldChange(name, RequiredText(field, "type"), field.GetProperty("value")));
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

    public static class LiveFieldValues
    {
        public const int MaxDepth = 8;
        public const int MaxElements = 256;
        public const int MaxNodes = 1024;
        public delegate bool LeafDecoder(Type type, JsonElement json, out object value);

        /// <summary>Builds a detached replacement. Never invokes user constructors or accessors.</summary>
        public static object Decode(Type type, JsonElement json, Func<Type, IEnumerable<FieldInfo>> fields,
            LeafDecoder leaf = null)
        {
            int nodes = 0;
            return Decode(type, json, fields, leaf, 0, ref nodes);
        }

        private static object Decode(Type type, JsonElement json, Func<Type, IEnumerable<FieldInfo>> fields,
            LeafDecoder leaf, int depth, ref int nodes)
        {
            CheckBounds(depth, ++nodes);
            if (TryDecodeScalar(type, json, out var value)) return value;
            if (leaf != null && leaf(type, json, out value)) return value;
            bool collection = TryCollection(type, out Type element);
            if (!collection) RequireDataType(type);
            if (json.ValueKind == JsonValueKind.Null && !type.IsValueType) return null;
            if (collection)
            {
                if (json.ValueKind != JsonValueKind.Array || json.GetArrayLength() > MaxElements)
                    throw new ArgumentException("Collection requires a JSON array of at most 256 elements.");
                int count = json.GetArrayLength();
                IList result = type.IsArray ? (IList)Array.CreateInstance(element, count) : (IList)Activator.CreateInstance(type);
                for (int i = 0; i < count; i++)
                {
                    object item = Decode(element, json[i], fields, leaf, depth + 1, ref nodes);
                    if (type.IsArray) result[i] = item;
                    else result.Add(item);
                }
                return result;
            }
            if (json.ValueKind != JsonValueKind.Object) throw new ArgumentException("Data field requires a JSON object.");
            var members = new List<FieldInfo>(fields(type));
            if (json.GetPropertyCount() != members.Count)
                throw new ArgumentException("Data replacement must contain exactly its serializable fields: " + type.FullName);
            object instance = FormatterServices.GetUninitializedObject(type);
            foreach (var field in members)
                field.SetValue(instance, Decode(field.FieldType, json.GetProperty(field.Name), fields, leaf, depth + 1, ref nodes));
            return instance;
        }

        public static void CheckBounds(int depth, int nodes)
        {
            if (depth > MaxDepth || nodes > MaxNodes)
                throw new NotSupportedException("Live fields exceed depth 8 or 1024 value nodes.");
        }

        public static bool TryCollection(Type type, out Type element)
        {
            element = null;
            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1 || type != type.GetElementType().MakeArrayType())
                    throw new NotSupportedException("Only one-dimensional arrays are supported.");
                element = type.GetElementType();
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                element = type.GetGenericArguments()[0];
            if (element == null) return false;
            if (typeof(IEnumerable).IsAssignableFrom(element) && element != typeof(string))
                throw new NotSupportedException("Wrap nested collections in a Serializable data class for Unity serialization.");
            return true;
        }

        public static void RequireDataType(Type type)
        {
            if (!type.IsDefined(typeof(SerializableAttribute), false) || type.IsAbstract || type.IsInterface
                || type.IsPrimitive || type.IsEnum || type.IsGenericType || type == typeof(object)
                || typeof(Delegate).IsAssignableFrom(type) || typeof(IEnumerable).IsAssignableFrom(type)
                || (type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
                || type.Namespace == "System" || type.Namespace?.StartsWith("System.", StringComparison.Ordinal) == true)
                throw new NotSupportedException("Unsupported live data type: " + type.FullName);
        }

        /// <summary>Persistence identity excludes transient assembly names inside List arguments.</summary>
        public static string PersistedTypeName(Type type)
        {
            if (TryCollection(type, out var element))
                return type.IsArray ? PersistedTypeName(element) + "[]" : "System.Collections.Generic.List<" + PersistedTypeName(element) + ">";
            return type.FullName;
        }

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
