using System;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 三维包围盒结构体（引擎无关，纯 C#）。
    /// </summary>
    public struct YokiBounds : IEquatable<YokiBounds>
    {
        public YokiVector3 Center;
        public YokiVector3 Size;

        public YokiBounds(YokiVector3 center, YokiVector3 size)
        {
            Center = center;
            Size = size;
        }

        public YokiVector3 Extents
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Size * 0.5f;
        }

        public YokiVector3 Min
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Center - Extents;
        }

        public YokiVector3 Max
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Center + Extents;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(YokiVector3 point)
        {
            var min = Min;
            var max = Max;
            return point.X >= min.X && point.X <= max.X &&
                   point.Y >= min.Y && point.Y <= max.Y &&
                   point.Z >= min.Z && point.Z <= max.Z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Intersects(YokiBounds other)
        {
            var min = Min;
            var max = Max;
            var otherMin = other.Min;
            var otherMax = other.Max;
            return otherMax.X >= min.X &&
                   otherMin.X <= max.X &&
                   otherMax.Y >= min.Y &&
                   otherMin.Y <= max.Y &&
                   otherMax.Z >= min.Z &&
                   otherMin.Z <= max.Z;
        }

        public bool Equals(YokiBounds other)
        {
            return Center == other.Center && Size == other.Size;
        }

        public override bool Equals(object obj) => obj is YokiBounds other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Center, Size);
        public static bool operator ==(YokiBounds a, YokiBounds b) => a.Equals(b);
        public static bool operator !=(YokiBounds a, YokiBounds b) => !a.Equals(b);
        public override string ToString() => $"Bounds(Center: {Center}, Size: {Size})";
    }
}
