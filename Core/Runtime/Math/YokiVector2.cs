using System;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 引擎无关的二维向量值类型，供 Core 和 Tool Runtime 共享空间数学。
    /// </summary>
    public struct YokiVector2 : IEquatable<YokiVector2>
    {
        /// <summary>获取或设置 X 分量。</summary>
        public float X;

        /// <summary>获取或设置 Y 分量。</summary>
        public float Y;

        /// <summary>表示零向量。</summary>
        public static readonly YokiVector2 Zero = new YokiVector2(0f, 0f);

        /// <summary>创建二维向量。</summary>
        /// <param name="x">X 分量。</param>
        /// <param name="y">Y 分量。</param>
        public YokiVector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>计算向量长度的平方，避免不必要的平方根。</summary>
        public float SqrMagnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return X * X + Y * Y; }
        }

        /// <summary>返回两个向量的和。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector2 operator +(YokiVector2 left, YokiVector2 right)
        {
            return new YokiVector2(left.X + right.X, left.Y + right.Y);
        }

        /// <summary>返回两个向量的差。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector2 operator -(YokiVector2 left, YokiVector2 right)
        {
            return new YokiVector2(left.X - right.X, left.Y - right.Y);
        }

        /// <summary>返回向量与标量的乘积。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiVector2 operator *(YokiVector2 value, float scale)
        {
            return new YokiVector2(value.X * scale, value.Y * scale);
        }

        /// <summary>判断两个向量的三个分量是否完全相等。</summary>
        public bool Equals(YokiVector2 other)
        {
            return X == other.X && Y == other.Y;
        }

        /// <summary>判断对象是否为相同二维向量。</summary>
        public override bool Equals(object obj)
        {
            return obj is YokiVector2 other && Equals(other);
        }

        /// <summary>返回二维向量的哈希值。</summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        /// <summary>比较两个二维向量。</summary>
        public static bool operator ==(YokiVector2 left, YokiVector2 right)
        {
            return left.Equals(right);
        }

        /// <summary>比较两个二维向量是否不同。</summary>
        public static bool operator !=(YokiVector2 left, YokiVector2 right)
        {
            return !left.Equals(right);
        }

        /// <summary>返回便于诊断的二维向量文本。</summary>
        public override string ToString()
        {
            return "(" + X.ToString("F2") + ", " + Y.ToString("F2") + ")";
        }
    }
}
