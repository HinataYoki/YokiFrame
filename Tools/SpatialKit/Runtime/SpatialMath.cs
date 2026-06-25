using System;
using System.Runtime.CompilerServices;
using YokiFrame;

namespace YokiFrame
{
    internal static class SpatialMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int FloorToInt(float value)
        {
            int integer = (int)value;
            return value < integer ? integer - 1 : integer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int CeilToInt(float value)
        {
            int integer = (int)value;
            return value > integer ? integer + 1 : integer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static YokiVector3 Clamp(YokiVector3 value, YokiVector3 min, YokiVector3 max)
        {
            return new YokiVector3(
                Clamp(value.X, min.X, max.X),
                Clamp(value.Y, min.Y, max.Y),
                Clamp(value.Z, min.Z, max.Z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IntersectsCircle(YokiRect rect, float centerX, float centerY, float radius)
        {
            float closestX = Clamp(centerX, rect.XMin, rect.XMax);
            float closestY = Clamp(centerY, rect.YMin, rect.YMax);
            float dx = centerX - closestX;
            float dy = centerY - closestY;
            return dx * dx + dy * dy <= radius * radius;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IntersectsSphere(YokiBounds bounds, YokiVector3 center, float radius)
        {
            var closest = Clamp(center, bounds.Min, bounds.Max);
            return (center - closest).SqrMagnitude <= radius * radius;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsUnboundedDistance(float maxDistance)
        {
            return maxDistance == float.MaxValue || float.IsInfinity(maxDistance);
        }
    }
}
