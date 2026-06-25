using System;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 三维向量结构体（引擎无关，纯 C#）
    /// </summary>
    public struct YokiVector3 : IEquatable<YokiVector3>
    {
        public float X;
        public float Y;
        public float Z;

        public static readonly YokiVector3 Zero = new(0f, 0f, 0f);
        public static readonly YokiVector3 One = new(1f, 1f, 1f);
        public static readonly YokiVector3 Up = new(0f, 1f, 0f);
        public static readonly YokiVector3 Down = new(0f, -1f, 0f);
        public static readonly YokiVector3 Left = new(-1f, 0f, 0f);
        public static readonly YokiVector3 Right = new(1f, 0f, 0f);
        public static readonly YokiVector3 Forward = new(0f, 0f, 1f);
        public static readonly YokiVector3 Back = new(0f, 0f, -1f);

        public YokiVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float Magnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MathF.Sqrt(X * X + Y * Y + Z * Z);
        }

        public float SqrMagnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => X * X + Y * Y + Z * Z;
        }

        public YokiVector3 Normalized
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var mag = Magnitude;
                return mag > 1E-05f ? new YokiVector3(X / mag, Y / mag, Z / mag) : Zero;
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
                Z /= mag;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Dot(YokiVector3 a, YokiVector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector3 Cross(YokiVector3 a, YokiVector3 b)
            => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Distance(YokiVector3 a, YokiVector3 b) => (a - b).Magnitude;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector3 Lerp(YokiVector3 a, YokiVector3 b, float t)
            => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector3 operator +(YokiVector3 a, YokiVector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector3 operator -(YokiVector3 a, YokiVector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector3 operator *(YokiVector3 a, float d) => new(a.X * d, a.Y * d, a.Z * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector3 operator *(float d, YokiVector3 a) => new(a.X * d, a.Y * d, a.Z * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector3 operator /(YokiVector3 a, float d) => new(a.X / d, a.Y / d, a.Z / d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector3 operator -(YokiVector3 a) => new(-a.X, -a.Y, -a.Z);

        public bool Equals(YokiVector3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is YokiVector3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public static bool operator ==(YokiVector3 a, YokiVector3 b) => a.Equals(b);
        public static bool operator !=(YokiVector3 a, YokiVector3 b) => !a.Equals(b);
        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }
}
