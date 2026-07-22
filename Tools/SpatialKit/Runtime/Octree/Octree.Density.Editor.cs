#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>为 Octree 提供 Editor/Tools 按需投影密度聚合，不进入 Player。</summary>
    public sealed partial class Octree<T> : ISpatialDensityDiagnostics, ISpatialIndexDiagnostics where T : ISpatialEntity
    {
        /// <summary>把三维实体按 XZ 投影聚合到二维 bin。</summary>
        /// <param name="resolution">每个轴的 bin 数。</param>
        /// <returns>当前 Octree 的二维投影密度快照。</returns>
        SpatialDensitySnapshot ISpatialDensityDiagnostics.CreateDensitySnapshot(int resolution)
        {
            resolution = Math.Max(4, Math.Min(64, resolution));
            int[] counts = new int[resolution * resolution];
            YokiBounds bounds = mRoot.Bounds;
            foreach (T entity in mEntities.Values)
            {
                YokiVector3 position = entity.Position;
                int x = ComputeBin(position.X, bounds.Min.X, bounds.Max.X, resolution);
                int y = ComputeBin(position.Z, bounds.Min.Z, bounds.Max.Z, resolution);
                counts[y * resolution + x]++;
            }

            return new SpatialDensitySnapshot(
                DiagnosticsId,
                IndexKind,
                SpatialPlane.XZ,
                resolution,
                counts,
                bounds.Min.X,
                bounds.Min.Z,
                bounds.Max.X,
                bounds.Max.Z);
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
