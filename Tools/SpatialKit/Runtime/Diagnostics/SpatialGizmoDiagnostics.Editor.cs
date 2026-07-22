#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>定义 Editor/Tools 空间几何快照入口，不向 Player 暴露实体或节点实现。</summary>
    internal interface ISpatialGizmoDiagnostics
    {
        /// <summary>按有界预算复制当前索引的节点和实体位置。</summary>
        /// <param name="maxNodes">允许复制的最大节点数量。</param>
        /// <param name="maxEntities">允许复制的最大实体数量。</param>
        /// <returns>不持有业务实体引用的只读几何快照。</returns>
        SpatialGizmoIndexSnapshot CreateGizmoSnapshot(int maxNodes, int maxEntities);
    }

    /// <summary>描述一个二维或三维空间节点的只读几何。</summary>
    internal readonly struct SpatialGizmoNodeSnapshot
    {
        /// <summary>创建二维投影节点。</summary>
        internal SpatialGizmoNodeSnapshot(
            YokiRect bounds,
            SpatialPlane plane,
            int depth,
            int entityCount,
            bool isLeaf)
        {
            IsVolume = false;
            Bounds2D = bounds;
            Bounds3D = default(YokiBounds);
            Plane = plane;
            Depth = depth;
            EntityCount = entityCount;
            IsLeaf = isLeaf;
        }

        /// <summary>创建三维体积节点。</summary>
        internal SpatialGizmoNodeSnapshot(
            YokiBounds bounds,
            int depth,
            int entityCount,
            bool isLeaf)
        {
            IsVolume = true;
            Bounds2D = default(YokiRect);
            Bounds3D = bounds;
            Plane = SpatialPlane.XZ;
            Depth = depth;
            EntityCount = entityCount;
            IsLeaf = isLeaf;
        }

        /// <summary>获取当前节点是否为三维体积。</summary>
        internal bool IsVolume { get; }

        /// <summary>获取二维投影边界。</summary>
        internal YokiRect Bounds2D { get; }

        /// <summary>获取三维体积边界。</summary>
        internal YokiBounds Bounds3D { get; }

        /// <summary>获取二维节点使用的投影平面。</summary>
        internal SpatialPlane Plane { get; }

        /// <summary>获取节点深度。</summary>
        internal int Depth { get; }

        /// <summary>获取叶节点直接持有的实体数量。</summary>
        internal int EntityCount { get; }

        /// <summary>获取当前节点是否为叶节点。</summary>
        internal bool IsLeaf { get; }
    }

    /// <summary>保存一个实体的稳定编号与快照位置。</summary>
    internal readonly struct SpatialGizmoEntitySnapshot
    {
        /// <summary>创建不持有业务实体引用的位置快照。</summary>
        internal SpatialGizmoEntitySnapshot(int spatialId, YokiVector3 position)
        {
            SpatialId = spatialId;
            Position = position;
        }

        /// <summary>获取实体稳定编号。</summary>
        internal int SpatialId { get; }

        /// <summary>获取采样时的实体位置。</summary>
        internal YokiVector3 Position { get; }
    }

    /// <summary>保存单个空间索引的一次有界 Scene Gizmo 快照。</summary>
    internal sealed class SpatialGizmoIndexSnapshot
    {
        /// <summary>创建单索引几何快照并保留预算截断状态。</summary>
        internal SpatialGizmoIndexSnapshot(
            string diagnosticsId,
            string indexKind,
            SpatialPlane plane,
            bool isVolume,
            IReadOnlyList<SpatialGizmoNodeSnapshot> nodes,
            IReadOnlyList<SpatialGizmoEntitySnapshot> entities,
            bool nodesTruncated,
            bool entitiesTruncated)
        {
            DiagnosticsId = diagnosticsId ?? string.Empty;
            IndexKind = indexKind ?? string.Empty;
            Plane = plane;
            IsVolume = isVolume;
            Nodes = nodes ?? Array.Empty<SpatialGizmoNodeSnapshot>();
            Entities = entities ?? Array.Empty<SpatialGizmoEntitySnapshot>();
            NodesTruncated = nodesTruncated;
            EntitiesTruncated = entitiesTruncated;
        }

        /// <summary>获取索引诊断编号。</summary>
        internal string DiagnosticsId { get; }

        /// <summary>获取索引类型。</summary>
        internal string IndexKind { get; }

        /// <summary>获取二维索引平面或三维显示的默认投影提示。</summary>
        internal SpatialPlane Plane { get; }

        /// <summary>获取当前索引是否使用三维体积节点。</summary>
        internal bool IsVolume { get; }

        /// <summary>获取有界节点快照。</summary>
        internal IReadOnlyList<SpatialGizmoNodeSnapshot> Nodes { get; }

        /// <summary>获取有界实体位置快照。</summary>
        internal IReadOnlyList<SpatialGizmoEntitySnapshot> Entities { get; }

        /// <summary>获取节点是否因预算被裁剪。</summary>
        internal bool NodesTruncated { get; }

        /// <summary>获取实体是否因预算被裁剪。</summary>
        internal bool EntitiesTruncated { get; }
    }

    /// <summary>保存一次跨索引 Gizmo 采样及其诊断版本。</summary>
    internal sealed class SpatialGizmoDiagnosticsFrame
    {
        /// <summary>创建与单调诊断版本绑定的几何帧。</summary>
        internal SpatialGizmoDiagnosticsFrame(long version, IReadOnlyList<SpatialGizmoIndexSnapshot> indexes)
        {
            Version = version;
            Indexes = indexes ?? Array.Empty<SpatialGizmoIndexSnapshot>();
        }

        /// <summary>获取采样对应的单调诊断版本。</summary>
        internal long Version { get; }

        /// <summary>获取当前存活且支持 Gizmo 的索引快照。</summary>
        internal IReadOnlyList<SpatialGizmoIndexSnapshot> Indexes { get; }
    }

    /// <summary>为 Unity/Godot 工具适配层提供有界几何快照聚合。</summary>
    internal static partial class SpatialKitDiagnosticsRegistry
    {
        /// <summary>复制当前仍存活的 Gizmo Provider，再在锁外生成有界快照。</summary>
        internal static SpatialGizmoDiagnosticsFrame CreateGizmoFrame(int maxNodes, int maxEntities)
        {
            int nodeBudget = Math.Max(1, maxNodes);
            int entityBudget = Math.Max(1, maxEntities);
            List<ISpatialGizmoDiagnostics> providers = new();
            lock (sLock)
            {
                for (int index = 0; index < sIndexes.Count; index++)
                {
                    if (sIndexes[index].TryGetTarget(out ISpatialIndexDiagnostics target)
                        && target is ISpatialGizmoDiagnostics provider)
                    {
                        providers.Add(provider);
                    }
                }
            }

            List<SpatialGizmoIndexSnapshot> snapshots = new(providers.Count);
            for (int index = 0; index < providers.Count; index++)
            {
                snapshots.Add(providers[index].CreateGizmoSnapshot(nodeBudget, entityBudget));
            }

            return new SpatialGizmoDiagnosticsFrame(Version, snapshots);
        }
    }
}
#endif
