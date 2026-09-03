using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 引擎无关的二维轴对齐矩形，宽高必须由使用方保持为非负值。
    /// </summary>
    public struct YokiRect : IEquatable<YokiRect>
    {
        /// <summary>矩形左下角 X 坐标。</summary>
        public float X;

        /// <summary>矩形左下角 Y 坐标。</summary>
        public float Y;

        /// <summary>矩形宽度。</summary>
        public float Width;

        /// <summary>矩形高度。</summary>
        public float Height;

        /// <summary>创建二维矩形。</summary>
        /// <param name="x">左下角 X 坐标。</param>
        /// <param name="y">左下角 Y 坐标。</param>
        /// <param name="width">非负宽度。</param>
        /// <param name="height">非负高度。</param>
        public YokiRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>获取 X 轴最小值。</summary>
        public float XMin { get { return X; } }

        /// <summary>获取 X 轴最大值。</summary>
        public float XMax { get { return X + Width; } }

        /// <summary>获取 Y 轴最小值。</summary>
        public float YMin { get { return Y; } }

        /// <summary>获取 Y 轴最大值。</summary>
        public float YMax { get { return Y + Height; } }

        /// <summary>获取矩形中心点。</summary>
        public YokiVector2 Center
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new YokiVector2(X + Width * 0.5f, Y + Height * 0.5f); }
        }

        /// <summary>判断点是否位于矩形闭边界内。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(YokiVector2 point)
        {
            return point.X >= XMin && point.X <= XMax && point.Y >= YMin && point.Y <= YMax;
        }

        /// <summary>判断两个矩形是否相交或接触边界。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(YokiRect other)
        {
            return other.XMax >= XMin && other.XMin <= XMax && other.YMax >= YMin && other.YMin <= YMax;
        }

        /// <summary>判断对象是否为相同矩形。</summary>
        public bool Equals(YokiRect other)
        {
            return X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
        }

        /// <summary>判断对象是否为相同矩形。</summary>
        public override bool Equals(object obj)
        {
            return obj is YokiRect other && Equals(other);
        }

        /// <summary>返回矩形的哈希值。</summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Width, Height);
        }

        /// <summary>比较两个矩形。</summary>
        public static bool operator ==(YokiRect left, YokiRect right)
        {
            return left.Equals(right);
        }

        /// <summary>比较两个矩形是否不同。</summary>
        public static bool operator !=(YokiRect left, YokiRect right)
        {
            return !left.Equals(right);
        }

        /// <summary>返回便于诊断的矩形文本。</summary>
        public override string ToString()
        {
            return "Rect(" + X.ToString("F2", CultureInfo.InvariantCulture) + ", " + Y.ToString("F2", CultureInfo.InvariantCulture) + ", " + Width.ToString("F2", CultureInfo.InvariantCulture) + ", " + Height.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }
    }
}
