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
            public int instanceId;
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

        public static FieldInfo RequireField(object instance, string name)
        {
            foreach (FieldInfo field in Fields(instance.GetType()))
                if (field.Name == name) return field;
            throw new ArgumentException("Only declared public or SerializeField fields can be tuned: " + name);
        }

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

        private void CaptureValue(Type type, object value, string path, bool persistent, int depth, HashSet<object> seen)
        {
            YokiFrameLiveFieldValues.CheckBounds(depth, values.Count + 1);
            var item = new Value { name = path, type = YokiFrameLiveFieldValues.PersistedTypeName(type) };
            values.Add(item);
            if (typeof(Object).IsAssignableFrom(type))
            {
                Object reference = (Object)value;
                if (reference != default)
                {
                    item.instanceId = reference.GetInstanceID();
                    item.globalId = GlobalObjectId.GetGlobalObjectIdSlow(reference).ToString();
                    if (persistent && (!IsPersistentId(item.globalId) || reference is YokiFrameUnityLiveBehaviourHost
                        || !UnityLiveSnapshotIdentities.IsSaved(reference, item.globalId)))
                        throw new NotSupportedException("Field " + path + " references a runtime-only object.");
                }
            }
            else if (type == typeof(string)) item.text = (string)value;
            else if (type == typeof(int) || type == typeof(float)
                || type == typeof(bool) || type.IsEnum)
                item.text = Convert.ToString(value, CultureInfo.InvariantCulture);
            else
            {
                var box = new VectorValue();
                if (type == typeof(Vector2)) box.v2 = (Vector2)value;
                else if (type == typeof(Vector3)) box.v3 = (Vector3)value;
                else if (type == typeof(Vector4)) box.v4 = (Vector4)value;
                else if (type == typeof(Quaternion)) box.rotation = (Quaternion)value;
                else if (type == typeof(Color)) box.color = (Color)value;
                else
                {
                    bool collection = YokiFrameLiveFieldValues.TryCollection(type, out var element);
                    if (!collection) YokiFrameLiveFieldValues.RequireDataType(type);
                    if (value == null) { item.kind = "null"; return; }
                    if (value.GetType() != type) throw new NotSupportedException("Polymorphic live field: " + path);
                    if (!type.IsValueType && !seen.Add(value))
                        throw new NotSupportedException("Shared or cyclic managed reference: " + path);
                    if (collection)
                    {
                        var list = (IList)value;
                        if (list.Count > YokiFrameLiveFieldValues.MaxElements)
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
        }

        public void Restore(object instance, bool persistent, bool exactFields = false,
            IReadOnlyDictionary<string, object> resolvedReferences = null)
        {
            var fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            foreach (FieldInfo field in Fields(instance.GetType())) fields.Add(field.Name, field);
            var remaining = new Dictionary<string, Value>(StringComparer.Ordinal);
            if (values == null || values.Count > YokiFrameLiveFieldValues.MaxNodes)
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

        private static object RestoreValue(Type type, string path, bool persistent, int depth,
            Dictionary<string, Value> remaining, IReadOnlyDictionary<string, object> resolvedReferences)
        {
            YokiFrameLiveFieldValues.CheckBounds(depth, 0);
            if (!remaining.TryGetValue(path, out var item)) throw new InvalidOperationException("Missing field: " + path);
            remaining.Remove(path);
            if (item.type != YokiFrameLiveFieldValues.PersistedTypeName(type))
                throw new InvalidOperationException("Field type changed: " + path);
            if (!typeof(Object).IsAssignableFrom(type) && (item.hasReference || item.instanceId != 0 || !string.IsNullOrEmpty(item.globalId)))
                throw new InvalidOperationException("Unexpected object reference: " + path);
            if (!string.IsNullOrEmpty(item.kind))
            {
                bool collection = YokiFrameLiveFieldValues.TryCollection(type, out var element);
                if (!collection) YokiFrameLiveFieldValues.RequireDataType(type);
                if (item.kind == "null" && !type.IsValueType && item.count == 0) return null;
                if (collection && item.kind == "collection")
                {
                    if (item.count < 0 || item.count > YokiFrameLiveFieldValues.MaxElements)
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
            if (item.count != 0) throw new InvalidOperationException("Invalid leaf field: " + path);
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
                else if (!persistent && item.instanceId != 0)
                    reference = EditorUtility.InstanceIDToObject(item.instanceId);
                if ((item.instanceId != 0 || !string.IsNullOrEmpty(item.globalId))
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

        public static bool IsRoot(string path) => path.IndexOf('.') < 0 && path.IndexOf('[') < 0;
        private static string ElementPath(string path, int index) => path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";

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

        public static void ValidateType(Type type) => ValidateType(type, new HashSet<Type>());

        private static void ValidateType(Type type, HashSet<Type> seen)
        {
            if (!seen.Add(type)) return;
            if (type == typeof(int) || type == typeof(float) || type == typeof(bool) || type == typeof(string)
                || type.IsEnum || typeof(Object).IsAssignableFrom(type) || type == typeof(Vector2)
                || type == typeof(Vector3) || type == typeof(Vector4) || type == typeof(Quaternion) || type == typeof(Color)) return;
            if (YokiFrameLiveFieldValues.TryCollection(type, out var element))
            {
                ValidateType(element, seen);
                return;
            }
            YokiFrameLiveFieldValues.RequireDataType(type);
            foreach (var field in Fields(type)) ValidateType(field.FieldType, seen);
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }

        public static bool IsPersistentId(string id)
        {
            if (!GlobalObjectId.TryParse(id, out GlobalObjectId value)) return false;
            return value.identifierType != 0 && value.targetObjectId != 0
                && value.assetGUID.ToString() != "00000000000000000000000000000000";
        }
    }
}
#endif
