using System;
using System.Collections.Generic;
using System.Globalization;
using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// 空间索引诊断接口。
    /// 只暴露轻量只读状态，供 CommandBridge、Tauri 和 AI snapshot 使用；运行时查询热路径不写文件。
    /// </summary>
    public interface ISpatialIndexDiagnostics
    {
        /// <summary>
        /// 获取诊断唯一编号。
        /// </summary>
        string DiagnosticsId { get; }

        /// <summary>
        /// 获取索引类型名称。
        /// </summary>
        string IndexKind { get; }

        /// <summary>
        /// 获取实体类型名称。
        /// </summary>
        string EntityTypeName { get; }

        /// <summary>
        /// 获取索引内实体数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 获取投影平面名称。
        /// </summary>
        string PlaneName { get; }

        /// <summary>
        /// 获取网格尺寸。
        /// </summary>
        float CellSize { get; }

        /// <summary>
        /// 获取树索引最大深度。
        /// </summary>
        int MaxDepth { get; }

        /// <summary>
        /// 获取单节点最大实体数。
        /// </summary>
        int MaxEntitiesPerNode { get; }

        /// <summary>
        /// 获取分区数量。
        /// </summary>
        int PartitionCount { get; }

        /// <summary>
        /// 获取诊断快照是否包含网格尺寸。
        /// </summary>
        bool HasCellSize { get; }

        /// <summary>
        /// 获取诊断快照是否包含二维边界。
        /// </summary>
        bool HasBounds2D { get; }

        /// <summary>
        /// 获取诊断快照是否包含三维边界。
        /// </summary>
        bool HasBounds3D { get; }

        /// <summary>
        /// 获取二维索引边界。
        /// </summary>
        YokiRect Bounds2D { get; }

        /// <summary>
        /// 获取三维索引边界。
        /// </summary>
        YokiBounds Bounds3D { get; }

        /// <summary>
        /// 获取索引创建时间。
        /// </summary>
        string CreatedAtUtc { get; }
    }

    internal static class SpatialKitDiagnosticsRegistry
    {
        private static readonly object sLock = new();
        private static readonly List<WeakReference<ISpatialIndexDiagnostics>> sIndexes = new();
        private static int sNextIndexId;
        private static int sTotalCreatedIndexCount;
        private static int sTotalReleasedIndexCount;

        internal static string NextIndexId(string prefix)
        {
            lock (sLock)
            {
                sNextIndexId++;
                var safePrefix = string.IsNullOrEmpty(prefix) ? "spatial" : prefix;
                return safePrefix + "-" + sNextIndexId.ToString(CultureInfo.InvariantCulture);
            }
        }

        internal static void Register(ISpatialIndexDiagnostics index)
        {
            if (index == null)
            {
                return;
            }

            lock (sLock)
            {
                sIndexes.Add(new WeakReference<ISpatialIndexDiagnostics>(index));
                sTotalCreatedIndexCount++;
            }
        }

        internal static SpatialKitDiagnosticsSnapshot CreateSnapshot()
        {
            lock (sLock)
            {
                var indexes = new List<SpatialIndexDiagnosticsSnapshot>(sIndexes.Count);
                int releasedCount = 0;

                for (int i = sIndexes.Count - 1; i >= 0; i--)
                {
                    ISpatialIndexDiagnostics index;
                    if (!sIndexes[i].TryGetTarget(out index) || index == null)
                    {
                        sIndexes.RemoveAt(i);
                        releasedCount++;
                        continue;
                    }

                    indexes.Add(SpatialIndexDiagnosticsSnapshot.From(index));
                }

                indexes.Reverse();
                sTotalReleasedIndexCount += releasedCount;
                return new SpatialKitDiagnosticsSnapshot(
                    sTotalCreatedIndexCount,
                    indexes,
                    sTotalReleasedIndexCount);
            }
        }
    }

    internal sealed class SpatialKitDiagnosticsSnapshot
    {
        internal SpatialKitDiagnosticsSnapshot(
            int totalCreatedIndexCount,
            List<SpatialIndexDiagnosticsSnapshot> indexes,
            int releasedIndexCount)
        {
            TotalCreatedIndexCount = totalCreatedIndexCount;
            Indexes = indexes ?? new List<SpatialIndexDiagnosticsSnapshot>();
            ReleasedIndexCount = releasedIndexCount;
        }

        internal int TotalCreatedIndexCount { get; }

        internal List<SpatialIndexDiagnosticsSnapshot> Indexes { get; }

        internal int ReleasedIndexCount { get; }
    }

    internal readonly struct SpatialIndexDiagnosticsSnapshot
    {
        internal SpatialIndexDiagnosticsSnapshot(
            string diagnosticsId,
            string indexKind,
            string entityTypeName,
            int count,
            string planeName,
            float cellSize,
            int maxDepth,
            int maxEntitiesPerNode,
            int partitionCount,
            bool hasCellSize,
            bool hasBounds2D,
            bool hasBounds3D,
            YokiRect bounds2D,
            YokiBounds bounds3D,
            string createdAtUtc)
        {
            DiagnosticsId = diagnosticsId ?? string.Empty;
            IndexKind = indexKind ?? string.Empty;
            EntityTypeName = entityTypeName ?? string.Empty;
            Count = count;
            PlaneName = planeName ?? string.Empty;
            CellSize = cellSize;
            MaxDepth = maxDepth;
            MaxEntitiesPerNode = maxEntitiesPerNode;
            PartitionCount = partitionCount;
            HasCellSize = hasCellSize;
            HasBounds2D = hasBounds2D;
            HasBounds3D = hasBounds3D;
            Bounds2D = bounds2D;
            Bounds3D = bounds3D;
            CreatedAtUtc = createdAtUtc ?? string.Empty;
        }

        internal string DiagnosticsId { get; }

        internal string IndexKind { get; }

        internal string EntityTypeName { get; }

        internal int Count { get; }

        internal string PlaneName { get; }

        internal float CellSize { get; }

        internal int MaxDepth { get; }

        internal int MaxEntitiesPerNode { get; }

        internal int PartitionCount { get; }

        internal bool HasCellSize { get; }

        internal bool HasBounds2D { get; }

        internal bool HasBounds3D { get; }

        internal YokiRect Bounds2D { get; }

        internal YokiBounds Bounds3D { get; }

        internal string CreatedAtUtc { get; }

        internal static SpatialIndexDiagnosticsSnapshot From(ISpatialIndexDiagnostics index)
        {
            return new SpatialIndexDiagnosticsSnapshot(
                index.DiagnosticsId,
                index.IndexKind,
                index.EntityTypeName,
                index.Count,
                index.PlaneName,
                index.CellSize,
                index.MaxDepth,
                index.MaxEntitiesPerNode,
                index.PartitionCount,
                index.HasCellSize,
                index.HasBounds2D,
                index.HasBounds3D,
                index.Bounds2D,
                index.Bounds3D,
                index.CreatedAtUtc);
        }
    }
}
