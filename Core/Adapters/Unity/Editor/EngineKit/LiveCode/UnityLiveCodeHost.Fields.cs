#if UNITY_EDITOR
using System;
using UnityEngine;
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed partial class UnityLiveCodeHost
    {
        public YokiFrameLiveFieldBinding BindTunableField(IDisposable attachment, string name)
        {
            var value = Require(attachment);
            if (!value.Fields.TryGetValue(name, out var field))
            {
                object instance = value.Host.Instance;
                value.Fields.Add(name, field = new YokiFrameLiveFieldBinding(instance, UnityLiveFieldState.RequireField(instance, name)));
            }
            return field;
        }

        public object DecodeTunableValue(Type type, JsonElement value)
        {
            UnityLiveFieldState.ValidateType(type);
            return YokiFrameLiveFieldValues.Decode(type, value, UnityLiveFieldState.Fields, TryDecodeUnityValue);
        }

        private static bool TryDecodeUnityValue(Type type, JsonElement value, out object result)
        {
            result = null;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                throw new NotSupportedException("JSON tuning does not accept object references; use trusted SetField.");
            if (type != typeof(Vector2) && type != typeof(Vector3) && type != typeof(Vector4)
                && type != typeof(Quaternion) && type != typeof(Color))
                return false;
            int size = type == typeof(Vector2) ? 2 : type == typeof(Vector3) ? 3 : 4;
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != size)
                throw new ArgumentException(type.Name + " requires a JSON numeric array of length " + size + ".");
            float x = YokiFrameLiveFieldValues.ReadSingle(value[0]);
            float y = YokiFrameLiveFieldValues.ReadSingle(value[1]);
            float z = size > 2 ? YokiFrameLiveFieldValues.ReadSingle(value[2]) : 0;
            float w = size > 3 ? YokiFrameLiveFieldValues.ReadSingle(value[3]) : 0;
            if (type == typeof(Vector2)) result = new Vector2(x, y);
            else if (type == typeof(Vector3)) result = new Vector3(x, y, z);
            else if (type == typeof(Vector4)) result = new Vector4(x, y, z, w);
            else if (type == typeof(Quaternion)) result = new Quaternion(x, y, z, w);
            else result = new Color(x, y, z, w);
            return true;
        }
    }
}
#endif
