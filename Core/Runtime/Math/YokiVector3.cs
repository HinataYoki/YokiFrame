using System;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 引擎无关的三维向量值类型，避免空间索引直接依赖宿主引擎。
    /// </summary>
    public struct YokiVector3 : IEquatable<YokiVector3>
    {
        /// <summary>获取或设置 X 分量。</summary>
        public float X;

        /// <summary>获取或设置 Y 分量。</summary>
        public float Y;

        /// <summary>获取或设置 Z 分量。</summary>
        public float Z;

        /// <summary>表示零向量。</summary>
        public static readonly YokiVector3 Zero = new YokiVector3(0f, 0f, 0f);

        /// <summary>创建三维向量。</summary>
        /// <param name="x">X 分量。</param>
        /// <param name="y">Y 分量。</param>
        /// <param name="z">Z 分量。</param>
        public YokiVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>计算向量长度的平方，避免不必要的平方根。</summary>
        public float SqrMagnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return X * X + Y * Y + Z * Z; }
        }

        /// <summary>返回两个向量的和。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector3 operator +(YokiVector3 left, YokiVector3 right)
        {
            return new YokiVector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        /// <summary>返回两个向量的差。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector3 operator -(YokiVector3 left, YokiVector3 right)
        {
            return new YokiVector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        /// <summary>返回向量与标量的乘积。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector3 operator *(YokiVector3 value, float scale)
        {
            return new YokiVector3(value.X * scale, value.Y * scale, value.Z * scale);
        }

        /// <summary>返回向量的相反数。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector3 operator -(YokiVector3 value)
        {
            return new YokiVector3(-value.X, -value.Y, -value.Z);
        }

        /// <summary>判断两个向量的三个分量是否完全相等。</summary>
        public bool Equals(YokiVector3 other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        /// <summary>判断对象是否为相同三维向量。</summary>
        public override bool Equals(object obj)
        {
            return obj is YokiVector3 other && Equals(other);
        }

        /// <summary>返回三维向量的哈希值。</summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        /// <summary>比较两个三维向量。</summary>
        public static bool operator ==(YokiVector3 left, YokiVector3 right)
        {
            return left.Equals(right);
        }

        /// <summary>比较两个三维向量是否不同。</summary>
        public static bool operator !=(YokiVector3 left, YokiVector3 right)
        {
            return !left.Equals(right);
        }

        /// <summary>返回便于诊断的三维向量文本。</summary>
        public override string ToString()
        {
            return "(" + X.ToString("F2") + ", " + Y.ToString("F2") + ", " + Z.ToString("F2") + ")";
        }
    }
}
