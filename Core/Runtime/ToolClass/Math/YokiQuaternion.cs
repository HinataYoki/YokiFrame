using System;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 四元数结构体（引擎无关，纯 C#）
    /// </summary>
    public struct YokiQuaternion : IEquatable<YokiQuaternion>
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public static readonly YokiQuaternion Identity = new(0f, 0f, 0f, 1f);

        public YokiQuaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>
        /// 从欧拉角（弧度）创建四元数（Yaw-Pitch-Roll）
        /// </summary>
        public static YokiQuaternion Euler(float roll, float pitch, float yaw)
        {
            float cy = MathF.Cos(yaw * 0.5f);
            float sy = MathF.Sin(yaw * 0.5f);
            float cp = MathF.Cos(pitch * 0.5f);
            float sp = MathF.Sin(pitch * 0.5f);
            float cr = MathF.Cos(roll * 0.5f);
            float sr = MathF.Sin(roll * 0.5f);

            return new YokiQuaternion(
                sr * cp * cy - cr * sp * sy,
                cr * sp * cy + sr * cp * sy,
                cr * cp * sy - sr * sp * cy,
                cr * cp * cy + sr * sp * sy
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiQuaternion Slerp(YokiQuaternion a, YokiQuaternion b, float t)
        {
            float dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
            if (dot < 0f)
            {
                b = new YokiQuaternion(-b.X, -b.Y, -b.Z, -b.W);
                dot = -dot;
            }

            if (dot > 0.9995f)
            {
                var result = new YokiQuaternion(
                    a.X + t * (b.X - a.X),
                    a.Y + t * (b.Y - a.Y),
                    a.Z + t * (b.Z - a.Z),
                    a.W + t * (b.W - a.W)
                );
                float invMag = 1f / MathF.Sqrt(result.X * result.X + result.Y * result.Y + result.Z * result.Z + result.W * result.W);
                return new YokiQuaternion(result.X * invMag, result.Y * invMag, result.Z * invMag, result.W * invMag);
            }

            float theta0 = MathF.Acos(dot);
            float theta = theta0 * t;
            float sinTheta = MathF.Sin(theta);
            float sinTheta0 = MathF.Sin(theta0);
            float s0 = MathF.Cos(theta) - dot * sinTheta / sinTheta0;
            float s1 = sinTheta / sinTheta0;

            return new YokiQuaternion(
                s0 * a.X + s1 * b.X,
                s0 * a.Y + s1 * b.Y,
                s0 * a.Z + s1 * b.Z,
                s0 * a.W + s1 * b.W
            );
        }

        public bool Equals(YokiQuaternion other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;
        public override bool Equals(object obj) => obj is YokiQuaternion other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
        public static bool operator ==(YokiQuaternion a, YokiQuaternion b) => a.Equals(b);
        public static bool operator !=(YokiQuaternion a, YokiQuaternion b) => !a.Equals(b);
        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2}, {W:F2})";
    }
}
