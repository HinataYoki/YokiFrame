using System;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 颜色结构体（引擎无关，纯 C#）
    /// </summary>
    public struct YokiColor : IEquatable<YokiColor>
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public static readonly YokiColor White = new(1f, 1f, 1f, 1f);
        public static readonly YokiColor Black = new(0f, 0f, 0f, 1f);
        public static readonly YokiColor Red = new(1f, 0f, 0f, 1f);
        public static readonly YokiColor Green = new(0f, 1f, 0f, 1f);
        public static readonly YokiColor Blue = new(0f, 0f, 1f, 1f);
        public static readonly YokiColor Yellow = new(1f, 1f, 0f, 1f);
        public static readonly YokiColor Cyan = new(0f, 1f, 1f, 1f);
        public static readonly YokiColor Magenta = new(1f, 0f, 1f, 1f);
        public static readonly YokiColor Gray = new(0.5f, 0.5f, 0.5f, 1f);
        public static readonly YokiColor Clear = new(0f, 0f, 0f, 0f);
        public static readonly YokiColor Orange = new(1f, 0.65f, 0f, 1f);

        public YokiColor(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YokiColor Lerp(YokiColor a, YokiColor b, float t)
            => new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);

        public bool Equals(YokiColor other) => R == other.R && G == other.G && B == other.B && A == other.A;
        public override bool Equals(object obj) => obj is YokiColor other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(R, G, B, A);
        public static bool operator ==(YokiColor a, YokiColor b) => a.Equals(b);
        public static bool operator !=(YokiColor a, YokiColor b) => !a.Equals(b);
        public override string ToString() => $"RGBA({R:F2}, {G:F2}, {B:F2}, {A:F2})";
    }
}
