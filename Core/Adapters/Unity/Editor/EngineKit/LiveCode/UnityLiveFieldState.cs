#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
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
            foreach (FieldInfo field in Fields(instance.GetType()))
            {
                object value = field.GetValue(instance);
                var item = new Value { name = field.Name, type = field.FieldType.FullName };
                if (typeof(Object).IsAssignableFrom(field.FieldType))
                {
                    Object reference = (Object)value;
                    if (reference != default)
                    {
                        item.instanceId = reference.GetInstanceID();
                        item.globalId = GlobalObjectId.GetGlobalObjectIdSlow(reference).ToString();
                        if (persistent && !IsPersistentId(item.globalId))
                            throw new NotSupportedException("Field " + field.Name + " references a runtime-only object.");
                    }
                }
                else if (field.FieldType == typeof(string)) item.text = (string)value;
                else if (field.FieldType == typeof(int) || field.FieldType == typeof(float)
                    || field.FieldType == typeof(bool) || field.FieldType.IsEnum)
                    item.text = Convert.ToString(value, CultureInfo.InvariantCulture);
                else
                {
                    var box = new VectorValue();
                    if (field.FieldType == typeof(Vector2)) box.v2 = (Vector2)value;
                    else if (field.FieldType == typeof(Vector3)) box.v3 = (Vector3)value;
                    else if (field.FieldType == typeof(Vector4)) box.v4 = (Vector4)value;
                    else if (field.FieldType == typeof(Quaternion)) box.rotation = (Quaternion)value;
                    else if (field.FieldType == typeof(Color)) box.color = (Color)value;
                    else throw new NotSupportedException("Unsupported persisted field " + field.Name + ": " + field.FieldType);
                    item.text = JsonUtility.ToJson(box);
                }
                result.values.Add(item);
            }
            return result;
        }

        public void Restore(object instance, bool persistent)
        {
            var fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            foreach (FieldInfo field in Fields(instance.GetType())) fields.Add(field.Name, field);
            foreach (Value item in values)
            {
                if (!fields.TryGetValue(item.name, out FieldInfo field)) continue;
                if (field.FieldType.FullName != item.type)
                    throw new InvalidOperationException("Field type changed: " + item.name);
                object value;
                Type type = field.FieldType;
                if (typeof(Object).IsAssignableFrom(type))
                {
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
                field.SetValue(instance, value);
            }
        }

        private static IEnumerable<FieldInfo> Fields(Type type)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (field.DeclaringType == type && !field.IsInitOnly && !field.IsNotSerialized
                    && (field.IsPublic || field.IsDefined(typeof(SerializeField), false))) yield return field;
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
