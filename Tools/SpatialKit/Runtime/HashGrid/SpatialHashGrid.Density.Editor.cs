#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>为 HashGrid 提供 Editor/Tools 按需密度聚合，不进入 Player。</summary>
    public sealed partial class SpatialHashGrid<T> : ISpatialDensityDiagnostics, ISpatialIndexDiagnostics where T : ISpatialEntity
    {
        /// <summary>按实际 cell 坐标范围聚合到固定二维 bin。</summary>
        /// <param name="resolution">每个轴的 bin 数。</param>
        /// <returns>当前 HashGrid 的二维密度快照。</returns>
        SpatialDensitySnapshot ISpatialDensityDiagnostics.CreateDensitySnapshot(int resolution)
        {
            resolution = Math.Max(4, Math.Min(64, resolution));
            if (mCells.Count == 0)
            {
                return new SpatialDensitySnapshot(
                    DiagnosticsId, IndexKind, mPlane, resolution,
                    new int[resolution * resolution], 0f, 0f, 1f, 1f);
            }

            int minA = int.MaxValue;
            int minB = int.MaxValue;
            int maxA = int.MinValue;
            int maxB = int.MinValue;
            foreach (long hash in mCells.Keys)
            {
                int cellA = (int)(hash >> 32);
                int cellB = (int)hash;
                minA = Math.Min(minA, cellA);
                minB = Math.Min(minB, cellB);
                maxA = Math.Max(maxA, cellA);
                maxB = Math.Max(maxB, cellB);
            }

            int[] counts = new int[resolution * resolution];
            foreach (var pair in mCells)
            {
                int cellA = (int)(pair.Key >> 32);
                int cellB = (int)pair.Key;
                int x = ComputeBin(cellA, minA, maxA, resolution);
                int y = ComputeBin(cellB, minB, maxB, resolution);
                counts[y * resolution + x] += pair.Value.Count;
            }

            return new SpatialDensitySnapshot(
                DiagnosticsId,
                IndexKind,
                mPlane,
                resolution,
                counts,
                minA * mCellSize,
                minB * mCellSize,
                (maxA + 1) * mCellSize,
                (maxB + 1) * mCellSize);
        }

        /// <summary>把离散 cell 坐标映射到固定分辨率 bin。</summary>
        private static int ComputeBin(int value, int min, int max, int resolution)
        {
            if (min == max)
            {
                return resolution / 2;
            }

            float normalized = (value - min) / (float)(max - min + 1);
            int bin = (int)(normalized * resolution);
            return Math.Max(0, Math.Min(resolution - 1, bin));
        }
    }
}
#endif
