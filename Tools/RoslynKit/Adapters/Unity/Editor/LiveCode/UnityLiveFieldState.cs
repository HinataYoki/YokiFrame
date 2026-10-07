#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    [Serializable]
    public sealed class UnityLiveFieldState
    {
        [Serializable]
        public sealed class Value
        {
            public string name;
            public string type;
            public string text;
            // 只在同一次 Play 会话内回填对象引用，不进入快照文件。
            // Unity 6.5 起对象身份是 64 位 EntityId，不能再存成 int。
#if UNITY_6000_5_OR_NEWER
            public UnityEngine.EntityId instanceId;
#else
            public int instanceId;
#endif
            public string globalId;
            public bool hasReference;
            public string kind;
            public int count;
        }

        [Serializable]
        private sealed class VectorValue
        {
            public Vector2 v2;
            public Vector3 v3;
            public Vector4 v4;
            public Quaternion rotation;
            public Color color;
        }

        public List<Value> values = new List<Value>();

        /// <summary>按声明类型查找可调字段。只接受该类型自己声明的公开字段或 SerializeField。</summary>
        /// <param name="instance">字段所属实例。</param>
        /// <param name="name">字段名。</param>
        /// <returns>匹配的字段。</returns>
        /// <exception cref="ArgumentException">字段不存在或不可调。</exception>
        public static FieldInfo RequireField(object instance, string name)
        {
            foreach (FieldInfo field in Fields(instance.GetType()))
                if (field.Name == name) return field;
            throw new ArgumentException("Only declared public or SerializeField fields can be tuned: " + name);
        }

        /// <summary>抓取实例上全部可调字段的当前值。先校验类型，再按字段声明顺序写入。</summary>
        /// <param name="instance">来源实例。</param>
        /// <param name="persistent">为 true 时拒绝只在运行时存在的对象引用。</param>
        /// <returns>新建的字段状态，不修改实例。</returns>
        public static UnityLiveFieldState Capture(object instance, bool persistent)
        {
            var result = new UnityLiveFieldState();
            var seen = new HashSet<object>(ReferenceComparer.Instance);
            foreach (FieldInfo field in Fields(instance.GetType()))
            {
                ValidateType(field.FieldType);
                result.CaptureValue(field.FieldType, field.GetValue(instance), field.Name, persistent, 0, seen);
            }
            return result;
        }

        /// <summary>
        /// 把一个值追加到状态。对象、字符串和标量就地写入；其余类型交给结构化抓取。
        /// 先占一个节点再读子值，嵌套调用共用同一个 seen。
        /// </summary>
        /// <param name="type">声明类型。</param>
        /// <param name="value">当前值。</param>
        /// <param name="path">字段路径。</param>
        /// <param name="persistent">是否要求引用能跨会话恢复。</param>
        /// <param name="depth">当前嵌套深度。</param>
        /// <param name="seen">已访问的引用，用于拒绝共享和环。</param>
        private void CaptureValue(Type type, object value, string path, bool persistent, int depth, HashSet<object> seen)
        {
            LiveFieldValues.CheckBounds(depth, values.Count + 1);
            var item = new Value { name = path, type = LiveFieldValues.PersistedTypeName(type) };
            values.Add(item);
            if (typeof(Object).IsAssignableFrom(type))
            {
                Object reference = (Object)value;
                if (reference != default)
                {
#if UNITY_6000_5_OR_NEWER
                    item.instanceId = reference.GetEntityId();
#else
                    item.instanceId = reference.GetInstanceID();
#endif
                    item.globalId = GlobalObjectId.GetGlobalObjectIdSlow(reference).ToString();
                    if (persistent && (!IsPersistentId(item.globalId) || reference is UnityLiveBehaviourHost
                        || !UnityLiveSnapshotIdentities.IsSaved(reference, item.globalId)))
                        throw new NotSupportedException("Field " + path + " references a runtime-only object.");
                }
            }
            else if (type == typeof(string)) item.text = (string)value;
            else if (type == typeof(int) || type == typeof(float)
                || type == typeof(bool) || type.IsEnum)
                item.text = Convert.ToString(value, CultureInfo.InvariantCulture);
            else CaptureStructured(item, type, value, path, persistent, depth, seen);
        }

        /// <summary>
        /// 抓取向量或嵌套数据。向量写成 JsonUtility 文本；集合和对象递归抓取后直接返回，不写 text。
        /// </summary>
        /// <param name="item">已经追加的当前节点。</param>
        /// <param name="type">声明类型。</param>
        /// <param name="value">当前值。</param>
        /// <param name="path">字段路径。</param>
        /// <param name="persistent">是否要求引用能跨会话恢复。</param>
        /// <param name="depth">当前嵌套深度。</param>
        /// <param name="seen">已访问的引用。</param>
        private void CaptureStructured(Value item, Type type, object value, string path, bool persistent, int depth,
            HashSet<object> seen)
        {
            var box = new VectorValue();
            if (type == typeof(Vector2)) box.v2 = (Vector2)value;
            else if (type == typeof(Vector3)) box.v3 = (Vector3)value;
            else if (type == typeof(Vector4)) box.v4 = (Vector4)value;
            else if (type == typeof(Quaternion)) box.rotation = (Quaternion)value;
            else if (type == typeof(Color)) box.color = (Color)value;
            else
            {
                bool collection = LiveFieldValues.TryCollection(type, out var element);
                if (!collection) LiveFieldValues.RequireDataType(type);
                if (value == null) { item.kind = "null"; return; }
                if (value.GetType() != type) throw new NotSupportedException("Polymorphic live field: " + path);
                if (!type.IsValueType && !seen.Add(value))
                    throw new NotSupportedException("Shared or cyclic managed reference: " + path);
                if (collection)
                {
                    var list = (IList)value;
                    if (list.Count > LiveFieldValues.MaxElements)
                        throw new NotSupportedException("Collection exceeds 256 elements: " + path);
                    item.kind = "collection";
                    item.count = list.Count;
                    for (int i = 0; i < list.Count; i++)
                        CaptureValue(element, list[i], ElementPath(path, i), persistent, depth + 1, seen);
                }
                else
                {
                    item.kind = "object";
                    foreach (var field in Fields(type))
                    {
                        item.count++;
                        CaptureValue(field.FieldType, field.GetValue(value), path + "." + field.Name, persistent, depth + 1, seen);
                    }
                }
                return;
            }

            item.text = JsonUtility.ToJson(box);
        }

        /// <summary>
        /// 把状态写回实例。先完整解码并校验字段集合，再逐个 SetValue；中途失败不会改目标。
        /// exactFields 为 true 时根字段必须与来源一一对应，未知根会失败而不是跳过。
        /// </summary>
        /// <param name="instance">写回目标。</param>
        /// <param name="persistent">为 true 时按全局 ID 恢复对象引用。</param>
        /// <param name="exactFields">为 true 时不允许快照少字段或多字段。</param>
        /// <param name="resolvedReferences">同一次播放里已解析的对象引用，键是字段路径。</param>
        public void Restore(object instance, bool persistent, bool exactFields = false,
            IReadOnlyDictionary<string, object> resolvedReferences = null)
        {
            var fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            foreach (FieldInfo field in Fields(instance.GetType())) fields.Add(field.Name, field);
            var remaining = new Dictionary<string, Value>(StringComparer.Ordinal);
            if (values == null || values.Count > LiveFieldValues.MaxNodes)
                throw new InvalidOperationException("Invalid live field state.");
            foreach (var item in values)
                if (item == null || string.IsNullOrEmpty(item.name) || remaining.ContainsKey(item.name))
                    throw new InvalidOperationException("Invalid or duplicate field path.");
                else remaining.Add(item.name, item);
            var bindings = new List<FieldInfo>();
            var replacements = new List<object>();
            int roots = 0;
            foreach (Value item in values)
            {
                if (!IsRoot(item.name)) continue;
                roots++;
                if (!fields.TryGetValue(item.name, out FieldInfo field))
                {
                    if (exactFields) throw new InvalidOperationException("Unknown snapshot field: " + item.name);
                    foreach (var skipped in values)
                        if (skipped.name == item.name || skipped.name.StartsWith(item.name + ".", StringComparison.Ordinal)
                            || skipped.name.StartsWith(item.name + "[", StringComparison.Ordinal)) remaining.Remove(skipped.name);
                    continue;
                }
                ValidateType(field.FieldType);
                bindings.Add(field);
                replacements.Add(RestoreValue(field.FieldType, item.name, persistent, 0, remaining, resolvedReferences));
            }
            if (remaining.Count != 0 || (exactFields && roots != fields.Count))
                throw new InvalidOperationException("Snapshot field set does not match the source.");
            // Decode the entire state before touching the destination, including nested references.
            for (int i = 0; i < bindings.Count; i++) bindings[i].SetValue(instance, replacements[i]);
        }

        /// <summary>
        /// 解码一个路径。先从 remaining 取走节点并核对类型，再按 kind 或叶子恢复。
        /// 非对象字段若带着引用标记会失败。
        /// </summary>
        /// <param name="type">声明类型。</param>
        /// <param name="path">字段路径。</param>
        /// <param name="persistent">是否按全局 ID 恢复引用。</param>
        /// <param name="depth">当前嵌套深度。</param>
        /// <param name="remaining">尚未消费的节点，成功匹配后会移除。</param>
        /// <param name="resolvedReferences">同一次播放里已解析的引用。</param>
        /// <returns>可写回的值。</returns>
        private static object RestoreValue(Type type, string path, bool persistent, int depth,
            Dictionary<string, Value> remaining, IReadOnlyDictionary<string, object> resolvedReferences)
        {
            LiveFieldValues.CheckBounds(depth, 0);
            if (!remaining.TryGetValue(path, out var item)) throw new InvalidOperationException("Missing field: " + path);
            remaining.Remove(path);
            if (item.type != LiveFieldValues.PersistedTypeName(type))
                throw new InvalidOperationException("Field type changed: " + path);
            if (!typeof(Object).IsAssignableFrom(type) && (item.hasReference || item.instanceId != default || !string.IsNullOrEmpty(item.globalId)))
                throw new InvalidOperationException("Unexpected object reference: " + path);
            if (!string.IsNullOrEmpty(item.kind))
                return RestoreShaped(type, path, item, persistent, depth, remaining, resolvedReferences);
            if (item.count != 0) throw new InvalidOperationException("Invalid leaf field: " + path);
            return RestoreLeaf(type, item, persistent, path, resolvedReferences);
        }

        /// <summary>
        /// 恢复带 kind 的空值、集合或数据对象。不是集合时先要求它是纯数据类型。
        /// 形状不匹配时抛 InvalidOperationException，不返回部分对象。
        /// </summary>
        /// <param name="type">声明类型。</param>
        /// <param name="path">字段路径。</param>
        /// <param name="item">当前节点，kind 非空。</param>
        /// <param name="persistent">是否按全局 ID 恢复引用。</param>
        /// <param name="depth">当前嵌套深度。</param>
        /// <param name="remaining">尚未消费的节点。</param>
        /// <param name="resolvedReferences">同一次播放里已解析的引用。</param>
        /// <returns>集合、数据对象或 null。</returns>
        private static object RestoreShaped(Type type, string path, Value item, bool persistent, int depth,
            Dictionary<string, Value> remaining, IReadOnlyDictionary<string, object> resolvedReferences)
        {
            bool collection = LiveFieldValues.TryCollection(type, out var element);
            if (!collection) LiveFieldValues.RequireDataType(type);
            if (item.kind == "null" && !type.IsValueType && item.count == 0) return null;
            if (collection && item.kind == "collection")
            {
                if (item.count < 0 || item.count > LiveFieldValues.MaxElements)
                    throw new InvalidOperationException("Invalid collection size: " + path);
                IList result = type.IsArray ? (IList)Array.CreateInstance(element, item.count) : (IList)Activator.CreateInstance(type);
                for (int i = 0; i < item.count; i++)
                {
                    object child = RestoreValue(element, ElementPath(path, i), persistent, depth + 1, remaining, resolvedReferences);
                    if (type.IsArray) result[i] = child;
                    else result.Add(child);
                }
                return result;
            }
            if (!collection && item.kind == "object")
            {
                var members = new List<FieldInfo>(Fields(type));
                if (item.count != members.Count) throw new InvalidOperationException("Data field set changed: " + path);
                object result = FormatterServices.GetUninitializedObject(type);
                foreach (var field in members)
                    field.SetValue(result, RestoreValue(field.FieldType, path + "." + field.Name, persistent,
                        depth + 1, remaining, resolvedReferences));
                return result;
            }
            throw new InvalidOperationException("Invalid field shape: " + path);
        }

        /// <summary>
        /// 恢复没有 kind 的叶子。对象引用优先用已解析结果，否则按持久或会话身份查找；
        /// 标量和向量沿用原来的转换，不支持的类型抛 NotSupportedException。
        /// </summary>
        /// <param name="type">声明类型。</param>
        /// <param name="item">叶子节点，count 必须已经是 0。</param>
        /// <param name="persistent">是否按全局 ID 恢复引用。</param>
        /// <param name="path">字段路径，供引用错误使用。</param>
        /// <param name="resolvedReferences">同一次播放里已解析的引用。</param>
        /// <returns>叶子值。</returns>
        private static object RestoreLeaf(Type type, Value item, bool persistent, string path,
            IReadOnlyDictionary<string, object> resolvedReferences)
        {
            object value;
            if (typeof(Object).IsAssignableFrom(type))
            {
                if (item.hasReference)
                {
                    if (resolvedReferences == null || !resolvedReferences.TryGetValue(path, out var resolved)
                        || !(resolved is Object referenceValue) || referenceValue == default || !type.IsInstanceOfType(resolved))
                        throw new InvalidOperationException("Object reference unavailable: " + path);
                    return resolved;
                }
                Object reference = null;
                if (persistent && !string.IsNullOrEmpty(item.globalId))
                {
                    if (!GlobalObjectId.TryParse(item.globalId, out GlobalObjectId global))
                        throw new InvalidOperationException("Invalid object reference: " + item.name);
                    reference = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(global);
                }
                else if (!persistent && item.instanceId != default)
#if UNITY_6000_5_OR_NEWER
                    reference = EditorUtility.EntityIdToObject(item.instanceId);
#else
                    reference = EditorUtility.InstanceIDToObject(item.instanceId);
#endif
                if ((item.instanceId != default || !string.IsNullOrEmpty(item.globalId))
                    && (reference == default || !type.IsInstanceOfType(reference)))
                    throw new InvalidOperationException("Object reference unavailable: " + item.name);
                value = reference;
            }
            else if (type == typeof(string)) value = item.text;
            else if (type.IsEnum) value = Enum.Parse(type, item.text);
            else if (type == typeof(int) || type == typeof(float) || type == typeof(bool))
                value = Convert.ChangeType(item.text, type, CultureInfo.InvariantCulture);
            else
            {
                var box = JsonUtility.FromJson<VectorValue>(item.text);
                if (type == typeof(Vector2)) value = box.v2;
                else if (type == typeof(Vector3)) value = box.v3;
                else if (type == typeof(Vector4)) value = box.v4;
                else if (type == typeof(Quaternion)) value = box.rotation;
                else if (type == typeof(Color)) value = box.color;
                else throw new NotSupportedException("Unsupported persisted field: " + item.name);
            }
            return value;
        }

        /// <summary>路径不含 '.' 和 '[' 时视为根字段。</summary>
        /// <param name="path">字段路径。</param>
        /// <returns>是根字段时返回 true。</returns>
        public static bool IsRoot(string path) => path.IndexOf('.') < 0 && path.IndexOf('[') < 0;

        /// <summary>生成集合元素路径，索引使用不变文化，避免小数点地区把下标写坏。</summary>
        /// <param name="path">父路径。</param>
        /// <param name="index">从 0 开始的下标。</param>
        /// <returns>形如 parent[0] 的路径。</returns>
        private static string ElementPath(string path, int index) => path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";

        /// <summary>
        /// 枚举该类型自己声明、可写且未标记 NonSerialized 的公开字段或 SerializeField。
        /// 遇到 SerializeReference 立即抛出，不继续枚举。
        /// </summary>
        /// <param name="type">声明类型。</param>
        /// <returns>可调字段。</returns>
        public static IEnumerable<FieldInfo> Fields(Type type)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.DeclaringType == type && !field.IsInitOnly && !field.IsNotSerialized
                    && field.IsDefined(typeof(SerializeReference), false))
                    throw new NotSupportedException("SerializeReference fields are not supported: " + field.Name);
                if (field.DeclaringType == type && !field.IsInitOnly && !field.IsNotSerialized
                    && (field.IsPublic || field.IsDefined(typeof(SerializeField), false))) yield return field;
            }
        }

        /// <summary>从新的已访问集合开始校验字段类型，供根字段入口使用。</summary>
        /// <param name="type">待校验类型。</param>
        public static void ValidateType(Type type) => ValidateType(type, new HashSet<Type>());

        /// <summary>
        /// 递归确认类型能被抓取和恢复。标量、枚举、Unity 对象和向量直接通过；
        /// 集合校验元素，数据对象校验每个字段。重复类型直接返回，避免环。
        /// </summary>
        /// <param name="type">待校验类型。</param>
        /// <param name="seen">当前这条类型链上已见过的类型。</param>
        private static void ValidateType(Type type, HashSet<Type> seen)
        {
            if (!seen.Add(type)) return;
            if (type == typeof(int) || type == typeof(float) || type == typeof(bool) || type == typeof(string)
                || type.IsEnum || typeof(Object).IsAssignableFrom(type) || type == typeof(Vector2)
                || type == typeof(Vector3) || type == typeof(Vector4) || type == typeof(Quaternion) || type == typeof(Color)) return;
            if (LiveFieldValues.TryCollection(type, out var element))
            {
                ValidateType(element, seen);
                return;
            }
            LiveFieldValues.RequireDataType(type);
            foreach (var field in Fields(type)) ValidateType(field.FieldType, seen);
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();

            /// <summary>按引用相等比较，供环和共享检测使用，不比较字段值。</summary>
            /// <param name="x">左引用。</param>
            /// <param name="y">右引用。</param>
            /// <returns>两者是同一引用时返回 true。</returns>
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);

            /// <summary>返回运行时引用哈希，与 Equals 的引用语义一致。</summary>
            /// <param name="value">被跟踪的对象。</param>
            /// <returns>引用哈希。</returns>
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }

        /// <summary>
        /// 判断全局 ID 是否指向可持久化资产。解析失败、类型或目标为 0、空 GUID 都不是持久 ID。
        /// </summary>
        /// <param name="id">GlobalObjectId 文本。</param>
        /// <returns>可以跨会话定位时返回 true。</returns>
        public static bool IsPersistentId(string id)
        {
            if (!GlobalObjectId.TryParse(id, out GlobalObjectId value)) return false;
            return value.identifierType != 0 && value.targetObjectId != 0
                && value.assetGUID.ToString() != "00000000000000000000000000000000";
        }
    }
}
#endif
