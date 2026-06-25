using System;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 二维矩形结构体（引擎无关，纯 C#）。
    /// </summary>
    public struct YokiRect : IEquatable<YokiRect>
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public YokiRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float XMin
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => X;
        }

        public float XMax
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => X + Width;
        }

        public float YMin
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Y;
        }

        public float YMax
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Y + Height;
        }

        public YokiVector2 Center
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new YokiVector2(X + Width * 0.5f, Y + Height * 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(YokiVector2 point)
        {
            return point.X >= XMin && point.X <= XMax &&
                   point.Y >= YMin && point.Y <= YMax;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(YokiRect other)
        {
            return other.XMax >= XMin &&
                   other.XMin <= XMax &&
                   other.YMax >= YMin &&
                   other.YMin <= YMax;
        }

        public bool Equals(YokiRect other)
        {
            return X == other.X &&
                   Y == other.Y &&
                   Width == other.Width &&
                   Height == other.Height;
        }

        public override bool Equals(object obj) => obj is YokiRect other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public static bool operator ==(YokiRect a, YokiRect b) => a.Equals(b);
        public static bool operator !=(YokiRect a, YokiRect b) => !a.Equals(b);
        public override string ToString() => $"Rect({X:F2}, {Y:F2}, {Width:F2}, {Height:F2})";
    }
}
