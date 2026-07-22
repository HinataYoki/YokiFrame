#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>为 Octree 复制真实三维节点和实体位置，供宿主 Gizmo 绘制。</summary>
    public sealed partial class Octree<T> : ISpatialGizmoDiagnostics where T : ISpatialEntity
    {
        /// <summary>按预算深度优先复制三维树节点与实体位置。</summary>
        SpatialGizmoIndexSnapshot ISpatialGizmoDiagnostics.CreateGizmoSnapshot(int maxNodes, int maxEntities)
        {
            List<SpatialGizmoNodeSnapshot> nodes = new(System.Math.Min(PartitionCount, maxNodes));
            AppendNodeSnapshots(mRoot, nodes, maxNodes);
            List<SpatialGizmoEntitySnapshot> entities = CopyEntitySnapshots(maxEntities);
            return new SpatialGizmoIndexSnapshot(
                DiagnosticsId,
                IndexKind,
                SpatialPlane.XZ,
                true,
                nodes,
                entities,
                nodes.Count < PartitionCount,
                entities.Count < mEntities.Count);
        }

        /// <summary>按父节点优先顺序复制三维节点，预算耗尽后停止递归。</summary>
        private static void AppendNodeSnapshots(
            OctreeNode node,
            List<SpatialGizmoNodeSnapshot> snapshots,
            int maxNodes)
        {
            if (snapshots.Count >= maxNodes)
            {
                return;
            }

            snapshots.Add(new SpatialGizmoNodeSnapshot(
                node.Bounds,
                node.Depth,
                node.Entities.Count,
                node.IsLeaf));
            IReadOnlyList<OctreeNode> children = node.Children;
            for (int index = 0; index < children.Count && snapshots.Count < maxNodes; index++)
            {
                AppendNodeSnapshots(children[index], snapshots, maxNodes);
            }
        }

        /// <summary>复制有界实体编号与位置，避免 Scene View 缓存业务实体引用。</summary>
        private List<SpatialGizmoEntitySnapshot> CopyEntitySnapshots(int maxEntities)
        {
            List<SpatialGizmoEntitySnapshot> snapshots = new(System.Math.Min(mEntities.Count, maxEntities));
            foreach (T entity in mEntities.Values)
            {
                if (snapshots.Count >= maxEntities)
                {
                    break;
                }

                snapshots.Add(new SpatialGizmoEntitySnapshot(entity.SpatialId, entity.Position));
            }

            return snapshots;
        }
    }
}
#endif
