#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// 暴露空间索引的轻量只读诊断信息，不包含实体列表或高频查询结果。
    /// </summary>
    public interface ISpatialIndexDiagnostics
    {
        /// <summary>获取进程内稳定的索引诊断编号。</summary>
        string DiagnosticsId { get; }

        /// <summary>获取 HashGrid、Quadtree 或 Octree 类型名称。</summary>
        string IndexKind { get; }

        /// <summary>获取实体 CLR 类型名称。</summary>
        string EntityTypeName { get; }

        /// <summary>获取当前索引实体数量。</summary>
        int Count { get; }

        /// <summary>获取投影平面名称；三维索引返回空字符串。</summary>
        string PlaneName { get; }

        /// <summary>获取网格尺寸；非 HashGrid 返回 0。</summary>
        float CellSize { get; }

        /// <summary>获取树最大深度；非树索引返回 0。</summary>
        int MaxDepth { get; }

        /// <summary>获取单节点最大实体数；非树索引返回 0。</summary>
        int MaxEntitiesPerNode { get; }

        /// <summary>获取当前分区或节点数量。</summary>
        int PartitionCount { get; }

        /// <summary>判断诊断数据是否包含网格尺寸。</summary>
        bool HasCellSize { get; }

        /// <summary>判断诊断数据是否包含二维边界。</summary>
        bool HasBounds2D { get; }

        /// <summary>判断诊断数据是否包含三维边界。</summary>
        bool HasBounds3D { get; }

        /// <summary>获取二维边界；没有二维边界时返回默认值。</summary>
        YokiRect Bounds2D { get; }

        /// <summary>获取三维边界；没有三维边界时返回默认值。</summary>
        YokiBounds Bounds3D { get; }

        /// <summary>获取索引创建时间的 UTC ISO 文本。</summary>
        string CreatedAtUtc { get; }
    }

    /// <summary>保存一次诊断采样中的索引摘要。</summary>
    public readonly struct SpatialIndexDiagnosticsSnapshot
    {
        /// <summary>创建索引诊断摘要。</summary>
        internal SpatialIndexDiagnosticsSnapshot(ISpatialIndexDiagnostics index)
        {
            DiagnosticsId = index.DiagnosticsId ?? string.Empty;
            IndexKind = index.IndexKind ?? string.Empty;
            EntityTypeName = index.EntityTypeName ?? string.Empty;
            Count = index.Count;
            PlaneName = index.PlaneName ?? string.Empty;
            CellSize = index.CellSize;
            MaxDepth = index.MaxDepth;
            MaxEntitiesPerNode = index.MaxEntitiesPerNode;
            PartitionCount = index.PartitionCount;
            HasCellSize = index.HasCellSize;
            HasBounds2D = index.HasBounds2D;
            HasBounds3D = index.HasBounds3D;
            Bounds2D = index.Bounds2D;
            Bounds3D = index.Bounds3D;
            CreatedAtUtc = index.CreatedAtUtc ?? string.Empty;
        }

        /// <summary>获取索引诊断编号。</summary>
        public string DiagnosticsId { get; }

        /// <summary>获取索引类型。</summary>
        public string IndexKind { get; }

        /// <summary>获取实体类型。</summary>
        public string EntityTypeName { get; }

        /// <summary>获取实体数量。</summary>
        public int Count { get; }

        /// <summary>获取投影平面名称。</summary>
        public string PlaneName { get; }

        /// <summary>获取网格尺寸。</summary>
        public float CellSize { get; }

        /// <summary>获取最大树深度。</summary>
        public int MaxDepth { get; }

        /// <summary>获取单节点实体上限。</summary>
        public int MaxEntitiesPerNode { get; }

        /// <summary>获取分区数量。</summary>
        public int PartitionCount { get; }

        /// <summary>判断是否有网格尺寸。</summary>
        public bool HasCellSize { get; }

        /// <summary>判断是否有二维边界。</summary>
        public bool HasBounds2D { get; }

        /// <summary>判断是否有三维边界。</summary>
        public bool HasBounds3D { get; }

        /// <summary>获取二维边界。</summary>
        public YokiRect Bounds2D { get; }

        /// <summary>获取三维边界。</summary>
        public YokiBounds Bounds3D { get; }

        /// <summary>获取创建时间。</summary>
        public string CreatedAtUtc { get; }
    }

    /// <summary>保存一次 SpatialKit 诊断采样的聚合统计。</summary>
    public sealed class SpatialKitDiagnosticsSnapshot
    {
        /// <summary>创建聚合诊断采样。</summary>
        internal SpatialKitDiagnosticsSnapshot(
            int totalCreatedIndexCount,
            IReadOnlyList<SpatialIndexDiagnosticsSnapshot> indexes,
            int releasedIndexCount)
        {
            TotalCreatedIndexCount = totalCreatedIndexCount;
            Indexes = indexes ?? Array.Empty<SpatialIndexDiagnosticsSnapshot>();
            ReleasedIndexCount = releasedIndexCount;
        }

        /// <summary>获取自进程启动以来创建的索引总数。</summary>
        public int TotalCreatedIndexCount { get; }

        /// <summary>获取当前仍存活的索引摘要。</summary>
        public IReadOnlyList<SpatialIndexDiagnosticsSnapshot> Indexes { get; }

        /// <summary>获取截至本次采样回收的弱引用数量。</summary>
        public int ReleasedIndexCount { get; }
    }

    /// <summary>维护索引弱引用并生成工具侧诊断采样。</summary>
    internal static partial class SpatialKitDiagnosticsRegistry
    {
        private static readonly object sLock = new object();
        private static readonly List<WeakReference<ISpatialIndexDiagnostics>> sIndexes =
            new List<WeakReference<ISpatialIndexDiagnostics>>();
        private static int sNextIndexId;
        private static int sTotalCreatedIndexCount;
        private static int sTotalReleasedIndexCount;
        private static long sVersion;

        /// <summary>获取索引集合或实体状态变化版本。</summary>
        internal static long Version { get { return Interlocked.Read(ref sVersion); } }

        /// <summary>标记一次会影响诊断快照的运行时变化。</summary>
        internal static void MarkChanged()
        {
            Interlocked.Increment(ref sVersion);
        }

        /// <summary>分配进程内唯一诊断编号。</summary>
        /// <param name="prefix">索引类型前缀。</param>
        /// <returns>稳定且适合展示的诊断编号。</returns>
        internal static string NextIndexId(string prefix)
        {
            lock (sLock)
            {
                sNextIndexId++;
                string safePrefix = string.IsNullOrEmpty(prefix) ? "spatial" : prefix;
                return safePrefix + "-" + sNextIndexId.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>登记新建索引，使直接构造和静态工厂都能进入诊断。</summary>
        /// <param name="index">待登记索引。</param>
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

        /// <summary>清理已回收索引并复制当前诊断信息。</summary>
        /// <returns>本次采样的聚合结果。</returns>
        internal static SpatialKitDiagnosticsSnapshot CreateSnapshot()
        {
            lock (sLock)
            {
                List<SpatialIndexDiagnosticsSnapshot> indexes =
                    new List<SpatialIndexDiagnosticsSnapshot>(sIndexes.Count);
                int releasedCount = 0;
                for (int index = sIndexes.Count - 1; index >= 0; index--)
                {
                    if (!sIndexes[index].TryGetTarget(out ISpatialIndexDiagnostics target) || target == null)
                    {
                        sIndexes.RemoveAt(index);
                        releasedCount++;
                        continue;
                    }

                    indexes.Add(new SpatialIndexDiagnosticsSnapshot(target));
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
}
#endif
