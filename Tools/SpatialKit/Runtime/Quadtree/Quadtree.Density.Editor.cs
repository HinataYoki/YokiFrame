#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>为 Quadtree 提供 Editor/Tools 按需密度聚合，不进入 Player。</summary>
    public sealed partial class Quadtree<T> : ISpatialDensityDiagnostics, ISpatialIndexDiagnostics where T : ISpatialEntity
    {
        /// <summary>按根边界把实体位置聚合到二维投影 bin。</summary>
        /// <param name="resolution">每个轴的 bin 数。</param>
        /// <returns>当前 Quadtree 的二维密度快照。</returns>
        SpatialDensitySnapshot ISpatialDensityDiagnostics.CreateDensitySnapshot(int resolution)
        {
            resolution = Math.Max(4, Math.Min(64, resolution));
            int[] counts = new int[resolution * resolution];
            YokiRect bounds = mRoot.Bounds;
            foreach (T entity in mEntities.Values)
            {
                YokiVector3 position = entity.Position;
                float coordinateB = SpatialMath.GetPlaneCoordinate(position, mPlane);
                int x = ComputeBin(position.X, bounds.X, bounds.XMax, resolution);
                int y = ComputeBin(coordinateB, bounds.Y, bounds.YMax, resolution);
                counts[y * resolution + x]++;
            }

            return new SpatialDensitySnapshot(
                DiagnosticsId,
                IndexKind,
                mPlane,
                resolution,
                counts,
                bounds.X,
                bounds.Y,
                bounds.XMax,
                bounds.YMax);
        }

        /// <summary>把投影坐标限制并映射到固定二维 bin。</summary>
        private static int ComputeBin(float value, float min, float max, int resolution)
        {
            float normalized = max <= min ? 0.5f : (value - min) / (max - min);
            int bin = (int)(Math.Max(0f, Math.Min(0.999999f, normalized)) * resolution);
            return Math.Max(0, Math.Min(resolution - 1, bin));
        }
    }
}
#endif
