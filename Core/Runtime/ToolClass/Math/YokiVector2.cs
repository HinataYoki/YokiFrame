using System;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 二维向量结构体（引擎无关，纯 C#）
    /// </summary>
    public struct YokiVector2 : IEquatable<YokiVector2>
    {
        public float X;
        public float Y;

        public static readonly YokiVector2 Zero = new(0f, 0f);
        public static readonly YokiVector2 One = new(1f, 1f);
        public static readonly YokiVector2 Up = new(0f, 1f);
        public static readonly YokiVector2 Down = new(0f, -1f);
        public static readonly YokiVector2 Left = new(-1f, 0f);
        public static readonly YokiVector2 Right = new(1f, 0f);

        public YokiVector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float Magnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MathF.Sqrt(X * X + Y * Y);
        }

        public float SqrMagnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => X * X + Y * Y;
        }

        public YokiVector2 Normalized
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var mag = Magnitude;
                return mag > 1E-05f ? new YokiVector2(X / mag, Y / mag) : Zero;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Normalize()
        {
            var mag = Magnitude;
            if (mag > 1E-05f)
            {
                X /= mag;
                Y /= mag;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Dot(YokiVector2 a, YokiVector2 b) => a.X * b.X + a.Y * b.Y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Distance(YokiVector2 a, YokiVector2 b) => (a - b).Magnitude;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector2 Lerp(YokiVector2 a, YokiVector2 b, float t)
            => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector2 operator +(YokiVector2 a, YokiVector2 b) => new(a.X + b.X, a.Y + b.Y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector2 operator -(YokiVector2 a, YokiVector2 b) => new(a.X - b.X, a.Y - b.Y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector2 operator *(YokiVector2 a, float d) => new(a.X * d, a.Y * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector2 operator *(float d, YokiVector2 a) => new(a.X * d, a.Y * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector2 operator /(YokiVector2 a, float d) => new(a.X / d, a.Y / d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector2 operator -(YokiVector2 a) => new(-a.X, -a.Y);

        public bool Equals(YokiVector2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is YokiVector2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static bool operator ==(YokiVector2 a, YokiVector2 b) => a.Equals(b);
        public static bool operator !=(YokiVector2 a, YokiVector2 b) => !a.Equals(b);
        public override string ToString() => $"({X:F2}, {Y:F2})";
    }
}
