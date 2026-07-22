#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>定义 Editor/Tools 按需生成空间密度聚合的内部边界。</summary>
    internal interface ISpatialDensityDiagnostics
    {
        /// <summary>按有界分辨率创建一次密度快照。</summary>
        /// <param name="resolution">每个轴的 bin 数，调用方负责限制范围。</param>
        /// <returns>不可变密度快照。</returns>
        SpatialDensitySnapshot CreateDensitySnapshot(int resolution);
    }

    /// <summary>表示一个空间密度热点 bin。</summary>
    internal readonly struct SpatialDensityHotspot
    {
        /// <summary>创建密度热点。</summary>
        internal SpatialDensityHotspot(int x, int y, int count)
        {
            X = x;
            Y = y;
            Count = count;
        }

        /// <summary>获取 bin 横坐标。</summary>
        internal int X { get; }

        /// <summary>获取 bin 纵坐标。</summary>
        internal int Y { get; }

        /// <summary>获取 bin 中实体数量。</summary>
        internal int Count { get; }
    }

    /// <summary>保存一次有界二维密度聚合，供 SnapshotWriter 序列化。</summary>
    internal sealed class SpatialDensitySnapshot
    {
        private const int MAX_HOTSPOTS = 8;

        /// <summary>使用已聚合的 bin 数组创建密度快照。</summary>
        internal SpatialDensitySnapshot(
            string diagnosticsId,
            string indexKind,
            SpatialPlane plane,
            int resolution,
            int[] counts,
            float minA,
            float minB,
            float maxA,
            float maxB)
        {
            DiagnosticsId = diagnosticsId;
            IndexKind = indexKind;
            Plane = plane;
            Resolution = resolution;
            Counts = counts;
            MinA = minA;
            MinB = minB;
            MaxA = maxA;
            MaxB = maxB;
            TotalBins = counts.Length;
            CalculateStatistics(counts, out int occupied, out int minimum, out int maximum, out int mean, out int p95);
            OccupiedBins = occupied;
            MinCount = minimum;
            MaxCount = maximum;
            MeanCount = mean;
            P95Count = p95;
            Hotspots = FindHotspots(counts, resolution);
        }

        /// <summary>获取所属索引诊断编号。</summary>
        internal string DiagnosticsId { get; }

        /// <summary>获取索引类型名称。</summary>
        internal string IndexKind { get; }

        /// <summary>获取投影平面。</summary>
        internal SpatialPlane Plane { get; }

        /// <summary>获取每个轴的 bin 分辨率。</summary>
        internal int Resolution { get; }

        /// <summary>获取行优先的 bin 数量数组。</summary>
        internal int[] Counts { get; }

        /// <summary>获取投影范围最小坐标。</summary>
        internal float MinA { get; }

        /// <summary>获取投影范围最小第二坐标。</summary>
        internal float MinB { get; }

        /// <summary>获取投影范围最大坐标。</summary>
        internal float MaxA { get; }

        /// <summary>获取投影范围最大第二坐标。</summary>
        internal float MaxB { get; }

        /// <summary>获取总 bin 数。</summary>
        internal int TotalBins { get; }

        /// <summary>获取非空 bin 数。</summary>
        internal int OccupiedBins { get; }

        /// <summary>获取最小非空占用。</summary>
        internal int MinCount { get; }

        /// <summary>获取平均占用。</summary>
        internal int MeanCount { get; }

        /// <summary>获取 P95 占用。</summary>
        internal int P95Count { get; }

        /// <summary>获取最大占用。</summary>
        internal int MaxCount { get; }

        /// <summary>获取按占用降序排列的热点 bin。</summary>
        internal IReadOnlyList<SpatialDensityHotspot> Hotspots { get; }

        /// <summary>计算占用统计，零实体时保持可解释的零值。</summary>
        private static void CalculateStatistics(
            int[] counts,
            out int occupied,
            out int minimum,
            out int maximum,
            out int mean,
            out int p95)
        {
            int[] sorted = new int[counts.Length];
            int total = 0;
            occupied = 0;
            minimum = int.MaxValue;
            maximum = 0;
            for (int index = 0; index < counts.Length; index++)
            {
                int count = counts[index];
                sorted[index] = count;
                total += count;
                if (count > 0)
                {
                    occupied++;
                    minimum = Math.Min(minimum, count);
                }

                maximum = Math.Max(maximum, count);
            }

            Array.Sort(sorted);
            mean = counts.Length == 0 ? 0 : (int)Math.Round((double)total / counts.Length);
            p95 = sorted.Length == 0 ? 0 : sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * 0.95d) - 1)];
            if (occupied == 0)
            {
                minimum = 0;
            }
        }

        /// <summary>使用固定容量选择热点，避免向 Workbench 发送完整排序表。</summary>
        private static IReadOnlyList<SpatialDensityHotspot> FindHotspots(int[] counts, int resolution)
        {
            List<SpatialDensityHotspot> hotspots = new List<SpatialDensityHotspot>(MAX_HOTSPOTS);
            for (int index = 0; index < counts.Length; index++)
            {
                int count = counts[index];
                if (count <= 0)
                {
                    continue;
                }

                SpatialDensityHotspot candidate = new SpatialDensityHotspot(index % resolution, index / resolution, count);
                int insertIndex = 0;
                while (insertIndex < hotspots.Count && hotspots[insertIndex].Count >= count)
                {
                    insertIndex++;
                }

                if (insertIndex >= MAX_HOTSPOTS && hotspots.Count == MAX_HOTSPOTS)
                {
                    continue;
                }

                if (insertIndex < hotspots.Count)
                {
                    hotspots.Insert(insertIndex, candidate);
                }
                else
                {
                    hotspots.Add(candidate);
                }

                if (hotspots.Count > MAX_HOTSPOTS)
                {
                    hotspots.RemoveAt(MAX_HOTSPOTS);
                }
            }

            return hotspots;
        }
    }

    /// <summary>为诊断注册表提供按需密度快照聚合。</summary>
    internal static partial class SpatialKitDiagnosticsRegistry
    {
        /// <summary>在不修改索引的前提下收集所有活跃索引密度。</summary>
        internal static IReadOnlyList<SpatialDensitySnapshot> CreateDensitySnapshots(int resolution)
        {
            List<ISpatialDensityDiagnostics> providers = new List<ISpatialDensityDiagnostics>();
            lock (sLock)
            {
                for (int index = sIndexes.Count - 1; index >= 0; index--)
                {
                    if (!sIndexes[index].TryGetTarget(out ISpatialIndexDiagnostics target) || target == null)
                    {
                        sIndexes.RemoveAt(index);
                        continue;
                    }

                    if (target is ISpatialDensityDiagnostics provider)
                    {
                        providers.Add(provider);
                    }
                }
            }

            List<SpatialDensitySnapshot> snapshots = new List<SpatialDensitySnapshot>(providers.Count);
            for (int index = 0; index < providers.Count; index++)
            {
                snapshots.Add(providers[index].CreateDensitySnapshot(resolution));
            }

            return snapshots;
        }
    }
}
#endif
