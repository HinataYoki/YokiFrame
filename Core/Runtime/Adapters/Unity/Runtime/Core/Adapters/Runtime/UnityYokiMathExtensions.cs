#if !GODOT
using System.Runtime.CompilerServices;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity 数学类型与 YokiFrame 引擎无关数学类型的转换扩展。
    /// </summary>
    public static class UnityYokiMathExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector2 ToYokiVector2(this Vector2 value)
        {
            return new YokiVector2(value.x, value.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ToUnityVector2(this YokiVector2 value)
        {
            return new Vector2(value.X, value.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector3 ToYokiVector3(this Vector3 value)
        {
            return new YokiVector3(value.x, value.y, value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ToUnityVector3(this YokiVector3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiBounds ToYokiBounds(this Bounds value)
        {
            return new YokiBounds(value.center.ToYokiVector3(), value.size.ToYokiVector3());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bounds ToUnityBounds(this YokiBounds value)
        {
            return new Bounds(value.Center.ToUnityVector3(), value.Size.ToUnityVector3());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiRect ToYokiRect(this Rect value)
        {
            return new YokiRect(value.x, value.y, value.width, value.height);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect ToUnityRect(this YokiRect value)
        {
            return new Rect(value.X, value.Y, value.Width, value.Height);
        }
    }
}
#endif
