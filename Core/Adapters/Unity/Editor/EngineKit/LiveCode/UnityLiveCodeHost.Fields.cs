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
            if (YokiFrameLiveFieldValues.TryDecodeScalar(type, value, out var scalar)) return scalar;
            if (type != typeof(Vector2) && type != typeof(Vector3) && type != typeof(Vector4)
                && type != typeof(Quaternion) && type != typeof(Color))
                throw new NotSupportedException("JSON tuning supports scalars, enums and Unity vectors/colors, not object references.");
            int size = type == typeof(Vector2) ? 2 : type == typeof(Vector3) ? 3 : 4;
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != size)
                throw new ArgumentException(type.Name + " requires a JSON numeric array of length " + size + ".");
            float x = YokiFrameLiveFieldValues.ReadSingle(value[0]);
            float y = YokiFrameLiveFieldValues.ReadSingle(value[1]);
            float z = size > 2 ? YokiFrameLiveFieldValues.ReadSingle(value[2]) : 0;
            float w = size > 3 ? YokiFrameLiveFieldValues.ReadSingle(value[3]) : 0;
            if (type == typeof(Vector2)) return new Vector2(x, y);
            if (type == typeof(Vector3)) return new Vector3(x, y, z);
            if (type == typeof(Vector4)) return new Vector4(x, y, z, w);
            if (type == typeof(Quaternion)) return new Quaternion(x, y, z, w);
            return new Color(x, y, z, w);
        }
    }
}
#endif
