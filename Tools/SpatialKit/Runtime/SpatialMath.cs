using System;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>提供空间索引内部共享的有限值、投影和相交计算。</summary>
    internal static class SpatialMath
    {
        /// <summary>判断浮点值是否可以安全参与空间分区计算。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>把有限浮点值向负无穷取整，并避免转换超出 int 范围。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int FloorToInt(float value)
        {
            if (value <= int.MinValue)
            {
                return int.MinValue;
            }

            if (value >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)MathF.Floor(value);
        }

        /// <summary>把有限浮点值向正无穷取整，并避免转换超出 int 范围。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int CeilToInt(float value)
        {
            if (value <= int.MinValue)
            {
                return int.MinValue;
            }

            if (value >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)MathF.Ceiling(value);
        }

        /// <summary>把标量限制在闭区间内。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        /// <summary>把三维点限制在包围盒范围内。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static YokiVector3 Clamp(YokiVector3 value, YokiVector3 min, YokiVector3 max)
        {
            return new YokiVector3(
                Clamp(value.X, min.X, max.X),
                Clamp(value.Y, min.Y, max.Y),
                Clamp(value.Z, min.Z, max.Z));
        }

        /// <summary>获取位置在选择平面上的第二个坐标。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float GetPlaneCoordinate(YokiVector3 position, SpatialPlane plane)
        {
            return plane == SpatialPlane.XZ ? position.Z : position.Y;
        }

        /// <summary>计算点到查询中心的投影平面距离平方。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float GetProjectedDistanceSquared(YokiVector3 position, YokiVector3 center, SpatialPlane plane)
        {
            float deltaA = position.X - center.X;
            float deltaB = GetPlaneCoordinate(position, plane) - GetPlaneCoordinate(center, plane);
            return deltaA * deltaA + deltaB * deltaB;
        }

        /// <summary>判断二维矩形是否与圆相交。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IntersectsCircle(YokiRect rect, float centerX, float centerY, float radius)
        {
            float closestX = Clamp(centerX, rect.XMin, rect.XMax);
            float closestY = Clamp(centerY, rect.YMin, rect.YMax);
            float deltaX = centerX - closestX;
            float deltaY = centerY - closestY;
            return deltaX * deltaX + deltaY * deltaY <= radius * radius;
        }

        /// <summary>计算二维点到矩形的最小距离平方，避免最近邻剪枝中的开方。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float DistanceSquaredToRect(YokiRect rect, float positionX, float positionY)
        {
            float deltaX = positionX < rect.XMin ? rect.XMin - positionX : positionX > rect.XMax ? positionX - rect.XMax : 0f;
            float deltaY = positionY < rect.YMin ? rect.YMin - positionY : positionY > rect.YMax ? positionY - rect.YMax : 0f;
            return deltaX * deltaX + deltaY * deltaY;
        }

        /// <summary>判断三维包围盒是否与球体相交。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IntersectsSphere(YokiBounds bounds, YokiVector3 center, float radius)
        {
            YokiVector3 closest = Clamp(center, bounds.Min, bounds.Max);
            return (center - closest).SqrMagnitude <= radius * radius;
        }

        /// <summary>计算三维点到包围盒的最小距离平方，避免最近邻剪枝中的开方。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float DistanceSquaredToBounds(YokiBounds bounds, YokiVector3 position)
        {
            YokiVector3 closest = Clamp(position, bounds.Min, bounds.Max);
            return (position - closest).SqrMagnitude;
        }

        /// <summary>判断距离是否表达无上限搜索。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsUnboundedDistance(float maxDistance)
        {
            return maxDistance == float.MaxValue || float.IsPositiveInfinity(maxDistance);
        }

        /// <summary>验证半径或距离是有限且非负的输入。</summary>
        internal static void ValidateNonNegativeFinite(float value, string parameterName)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and non-negative.");
            }
        }

        /// <summary>验证位置坐标全部为有限值，避免生成不可追踪的分区。</summary>
        internal static void ValidatePosition(YokiVector3 position, string parameterName)
        {
            if (!IsFinite(position.X) || !IsFinite(position.Y) || !IsFinite(position.Z))
            {
                throw new ArgumentException("Position must contain only finite coordinates.", parameterName);
            }
        }

        /// <summary>验证矩形边界为有限且正尺寸。</summary>
        internal static void ValidateRect(YokiRect bounds, string parameterName)
        {
            if (!IsFinite(bounds.X) || !IsFinite(bounds.Y) || !IsFinite(bounds.Width)
                || !IsFinite(bounds.Height) || bounds.Width <= 0f || bounds.Height <= 0f)
            {
                throw new ArgumentException("Bounds must contain finite coordinates and positive size.", parameterName);
            }
        }

        /// <summary>验证三维边界为有限且正尺寸。</summary>
        internal static void ValidateBounds(YokiBounds bounds, string parameterName)
        {
            if (!IsFinite(bounds.Center.X) || !IsFinite(bounds.Center.Y) || !IsFinite(bounds.Center.Z)
                || !IsFinite(bounds.Size.X) || !IsFinite(bounds.Size.Y) || !IsFinite(bounds.Size.Z)
                || bounds.Size.X <= 0f || bounds.Size.Y <= 0f || bounds.Size.Z <= 0f)
            {
                throw new ArgumentException("Bounds must contain finite coordinates and positive size.", parameterName);
            }
        }
    }
}
