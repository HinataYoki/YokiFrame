#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>为 HashGrid 复制已占用网格和实体位置，供宿主 Gizmo 绘制。</summary>
    public sealed partial class SpatialHashGrid<T> : ISpatialGizmoDiagnostics where T : ISpatialEntity
    {
        /// <summary>按预算复制已占用网格与实体位置，不持有业务实体引用。</summary>
        SpatialGizmoIndexSnapshot ISpatialGizmoDiagnostics.CreateGizmoSnapshot(int maxNodes, int maxEntities)
        {
            List<SpatialGizmoNodeSnapshot> nodes = new(System.Math.Min(mCells.Count, maxNodes));
            foreach (KeyValuePair<long, List<T>> pair in mCells)
            {
                if (nodes.Count >= maxNodes)
                {
                    break;
                }

                int cellA = (int)(pair.Key >> 32);
                int cellB = (int)pair.Key;
                YokiRect bounds = new(
                    cellA * mCellSize,
                    cellB * mCellSize,
                    mCellSize,
                    mCellSize);
                nodes.Add(new SpatialGizmoNodeSnapshot(bounds, mPlane, 0, pair.Value.Count, true));
            }

            List<SpatialGizmoEntitySnapshot> entities = CopyEntitySnapshots(maxEntities);
            return new SpatialGizmoIndexSnapshot(
                DiagnosticsId,
                IndexKind,
                mPlane,
                false,
                nodes,
                entities,
                nodes.Count < mCells.Count,
                entities.Count < mEntities.Count);
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
