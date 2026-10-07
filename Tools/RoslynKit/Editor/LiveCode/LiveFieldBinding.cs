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
        /// <summary>绑定一个可写实例字段。静态、只读和常量字段不能调。</summary>
        /// <param name="instance">字段所属实例。</param>
        /// <param name="field">实例字段。</param>
        public LiveFieldBinding(object instance, FieldInfo field)
        {
            if (instance == null || field == null || field.IsStatic || field.IsInitOnly || field.IsLiteral)
                throw new ArgumentException("A mutable instance field is required.");
            mInstance = instance; mField = field;
        }
        /// <summary>通过字段元数据读取当前值，不调用用户访问器。</summary>
        /// <returns>字段当前值。</returns>
        public object Read() => mField.GetValue(mInstance);
        /// <summary>通过字段元数据写入值，不调用用户访问器。</summary>
        /// <param name="value">新值，类型须能被字段接受。</param>
        public void Write(object value) => mField.SetValue(mInstance, value);
    }

    public interface ILiveFieldHost
    {
        /// <summary>按宿主白名单绑定可调实例字段。不得改走用户访问器。</summary>
        /// <param name="attachment">已挂接的行为。</param>
        /// <param name="name">字段名。</param>
        /// <returns>可直接读写的字段绑定。</returns>
        LiveFieldBinding BindTunableField(IDisposable attachment, string name);
        /// <summary>把 JSON 解码成字段类型的值。对象引用等宿主限制由实现拒绝。</summary>
        /// <param name="type">目标字段类型。</param>
        /// <param name="value">JSON 值。</param>
        /// <returns>可写入字段的值。</returns>
        object DecodeTunableValue(Type type, JsonElement value);
    }

    public sealed class LiveFieldChange
    {
        public string Name { get; }
        public string TypeName { get; }
        public JsonElement Value { get; }
        /// <summary>保存一次字段变更。JSON 元素的寿命依赖调用方持有的文档。</summary>
        /// <param name="name">字段名。</param>
        /// <param name="typeName">调用方声明的类型名。</param>
        /// <param name="value">JSON 值。</param>
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

        /// <summary>返回单个行为的字段变更。只读包装不复制 JSON 元素，文档寿命仍由请求持有。</summary>
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

        /// <summary>返回批次中的行为更新。解析完成后列表不再变化。</summary>
        public IReadOnlyList<LiveFieldUpdate> Updates => mUpdates.AsReadOnly();

        /// <summary>解析已确认的字段批次。要求 1 到 64 个行为、合计不超过 256 个字段，且标识和字段名不重复。</summary>
        /// <param name="json">字段更新 JSON，最大 48 KiB。</param>
        /// <returns>保留 JSON 值的请求，供随后按字段类型解码。</returns>
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

        /// <summary>读取非空且不超过 256 字符的字符串属性。</summary>
        /// <param name="json">所在对象。</param>
        /// <param name="name">属性名，同时出现在异常文案中。</param>
        /// <returns>属性文本。</returns>
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

        /// <summary>递归构造脱离原对象的替换值。集合用数组或 List，数据对象不调用用户构造函数。</summary>
        /// <param name="type">目标类型。</param>
        /// <param name="json">当前 JSON 节点。</param>
        /// <param name="fields">按类型提供可序列化字段。</param>
        /// <param name="leaf">宿主叶子解码器，可为 null。</param>
        /// <param name="depth">当前深度。</param>
        /// <param name="nodes">已访问节点数，成功和失败路径都会递增。</param>
        /// <returns>解码后的值；引用类型遇到 JSON null 时为 null。</returns>
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

        /// <summary>拒绝超过深度 8 或 1024 个值节点的结构。</summary>
        /// <param name="depth">当前深度。</param>
        /// <param name="nodes">当前节点数。</param>
        public static void CheckBounds(int depth, int nodes)
        {
            if (depth > MaxDepth || nodes > MaxNodes)
                throw new NotSupportedException("Live fields exceed depth 8 or 1024 value nodes.");
        }

        /// <summary>识别一维数组和 List。嵌套集合必须包在可序列化数据类中。</summary>
        /// <param name="type">待识别类型。</param>
        /// <param name="element">集合元素类型；不是受支持集合时为 null。</param>
        /// <returns>是受支持集合时为 true。</returns>
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

        /// <summary>只允许直接继承 object 或 ValueType 的具体可序列化数据类。系统命名空间、委托和枚举等不接受。</summary>
        /// <param name="type">待检查类型。</param>
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

        /// <summary>解码 string、int、float、bool 和枚举。类型不匹配时抛出，未识别的类型返回 false 且 value 为 null。</summary>
        /// <param name="type">目标类型。</param>
        /// <param name="json">JSON 节点。</param>
        /// <param name="value">解码结果。</param>
        /// <returns>已识别标量时为 true。</returns>
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

        /// <summary>把 JSON 数字读成有限 float。NaN、无穷和非数字都拒绝。</summary>
        /// <param name="json">JSON 节点。</param>
        /// <returns>范围内的有限单精度值。</returns>
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
