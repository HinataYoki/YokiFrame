using System;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 引擎无关的三维轴对齐包围盒，尺寸必须由使用方保持为非负值。
    /// </summary>
    public struct YokiBounds : IEquatable<YokiBounds>
    {
        /// <summary>包围盒中心点。</summary>
        public YokiVector3 Center;

        /// <summary>包围盒完整尺寸。</summary>
        public YokiVector3 Size;

        /// <summary>创建三维包围盒。</summary>
        /// <param name="center">中心点。</param>
        /// <param name="size">非负完整尺寸。</param>
        public YokiBounds(YokiVector3 center, YokiVector3 size)
        {
            Center = center;
            Size = size;
        }

        /// <summary>获取三个轴向的半尺寸。</summary>
        public YokiVector3 Extents
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Size * 0.5f; }
        }

        /// <summary>获取包围盒最小点。</summary>
        public YokiVector3 Min
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Center - Extents; }
        }

        /// <summary>获取包围盒最大点。</summary>
        public YokiVector3 Max
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Center + Extents; }
        }

        /// <summary>判断点是否位于包围盒闭边界内。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(YokiVector3 point)
        {
            YokiVector3 min = Min;
            YokiVector3 max = Max;
            return point.X >= min.X && point.X <= max.X
                && point.Y >= min.Y && point.Y <= max.Y
                && point.Z >= min.Z && point.Z <= max.Z;
        }

        /// <summary>判断两个包围盒是否相交或接触边界。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Intersects(YokiBounds other)
        {
            YokiVector3 min = Min;
            YokiVector3 max = Max;
            YokiVector3 otherMin = other.Min;
            YokiVector3 otherMax = other.Max;
            return otherMax.X >= min.X && otherMin.X <= max.X
                && otherMax.Y >= min.Y && otherMin.Y <= max.Y
                && otherMax.Z >= min.Z && otherMin.Z <= max.Z;
        }

        /// <summary>判断对象是否为相同包围盒。</summary>
        public bool Equals(YokiBounds other)
        {
            return Center == other.Center && Size == other.Size;
        }

        /// <summary>判断对象是否为相同包围盒。</summary>
        public override bool Equals(object obj)
        {
            return obj is YokiBounds other && Equals(other);
        }

        /// <summary>返回包围盒的哈希值。</summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(Center, Size);
        }

        /// <summary>比较两个包围盒。</summary>
        public static bool operator ==(YokiBounds left, YokiBounds right)
        {
            return left.Equals(right);
        }

        /// <summary>比较两个包围盒是否不同。</summary>
        public static bool operator !=(YokiBounds left, YokiBounds right)
        {
            return !left.Equals(right);
        }

        /// <summary>返回便于诊断的包围盒文本。</summary>
        public override string ToString()
        {
            return "Bounds(Center: " + Center + ", Size: " + Size + ")";
        }
    }
}
