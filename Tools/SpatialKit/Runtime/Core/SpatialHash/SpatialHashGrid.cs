using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 空间哈希网格实现
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <remarks>
    /// 将世界划分为固定大小的格子，适用于实体分布均匀的场景。
    /// 查询复杂度接近 O(1)，内存按需分配（仅存储非空格子）。
    /// </remarks>
    public class SpatialHashGrid<T> : ISpatialIndex<T> where T : ISpatialEntity
    {
        private readonly float mCellSize;
        private readonly float mInvCellSize;
        private readonly SpatialPlane mPlane;
        private readonly Dictionary<long, List<T>> mCells = new();
        private readonly Dictionary<int, long> mEntityToCell = new();
        private readonly Dictionary<int, T> mEntities = new();
        private readonly Stack<List<T>> mListPool = new();
        private int mCount;

        /// <inheritdoc/>
        public int Count => mCount;
        
        /// <summary>
        /// 当前使用的投影平面
        /// </summary>
        public SpatialPlane Plane => mPlane;

        /// <summary>
        /// 创建空间哈希网格
        /// </summary>
        /// <param name="cellSize">格子大小，建议等于最大查询半径</param>
        /// <param name="plane">投影平面，XZ 用于 2.5D，XY 用于传统 2D</param>
        public SpatialHashGrid(float cellSize, SpatialPlane plane = SpatialPlane.XZ)
        {
            if (cellSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(cellSize), "cellSize must be greater than 0");

            mCellSize = cellSize;
            mInvCellSize = 1f / cellSize;
            mPlane = plane;
        }

        /// <summary>
        /// 插入实体到空间哈希网格
        /// </summary>
        /// <param name="entity">要插入的实体，相同 SpatialId 会覆盖旧实体</param>
        public void Insert(T entity)
        {
            var id = entity.SpatialId;
            if (mEntityToCell.ContainsKey(id)) Remove(entity);

            var hash = ComputeHash(entity.Position);
            var cell = GetOrCreateCell(hash);
            cell.Add(entity);

            mEntityToCell[id] = hash;
            mEntities[id] = entity;
            mCount++;
        }

        /// <summary>
        /// 从空间哈希网格中移除实体
        /// </summary>
        /// <param name="entity">要移除的实体</param>
        /// <returns>移除成功返回 true，实体不存在返回 false</returns>
        public bool Remove(T entity)
        {
            var id = entity.SpatialId;
            if (!mEntityToCell.TryGetValue(id, out var hash)) return false;

            if (mCells.TryGetValue(hash, out var cell))
            {
                for (int i = cell.Count - 1; i >= 0; i--)
                {
                    if (cell[i].SpatialId == id)
                    {
                        cell.RemoveAt(i);
                        break;
                    }
                }
                if (cell.Count == 0)
                {
                    mCells.Remove(hash);
                    RecycleList(cell);
                }
            }

            mEntityToCell.Remove(id);
            mEntities.Remove(id);
            mCount--;
            return true;
        }

        /// <summary>
        /// 更新实体位置（实体移动后必须调用）
        /// </summary>
        /// <param name="entity">位置已变化的实体（struct 需创建新实例）</param>
        /// <remarks>内部自动处理跨格子迁移，同格子内移动仅更新引用</remarks>
        public void Update(T entity)
        {
            var id = entity.SpatialId;
            if (!mEntityToCell.TryGetValue(id, out var oldHash))
            {
                Insert(entity);
                return;
            }

            var newHash = ComputeHashFast(entity.Position);
            if (oldHash == newHash)
            {
                // 同格子内移动，只更新实体引用
                mEntities[id] = entity;
                if (mCells.TryGetValue(oldHash, out var cell))
                {
                    for (int i = 0; i < cell.Count; i++)
                    {
                        if (cell[i].SpatialId == id) { cell[i] = entity; break; }
                    }
                }
                return;
            }

            // 跨格子移动，需要迁移
            if (mCells.TryGetValue(oldHash, out var oldCell))
            {
                for (int i = oldCell.Count - 1; i >= 0; i--)
                {
                    if (oldCell[i].SpatialId == id) { oldCell.RemoveAt(i); break; }
                }
                if (oldCell.Count == 0) { mCells.Remove(oldHash); RecycleList(oldCell); }
            }

            var newCell = GetOrCreateCell(newHash);
            newCell.Add(entity);
            mEntityToCell[id] = newHash;
            mEntities[id] = entity;
        }
        
        /// <summary>
        /// 批量更新实体位置
        /// </summary>
        /// <param name="entities">位置已变化的实体列表</param>
        public void UpdateBatch(IReadOnlyList<T> entities)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                Update(entities[i]);
            }
        }

        /// <summary>
        /// 清空所有实体并回收内部资源
        /// </summary>
        public void Clear()
        {
            foreach (var cell in mCells.Values) { cell.Clear(); RecycleList(cell); }
            mCells.Clear();
            mEntityToCell.Clear();
            mEntities.Clear();
            mCount = 0;
        }

        private long ComputeHash(Vector3 position)
        {
            int cellA = Mathf.FloorToInt(position.x * mInvCellSize);
            int cellB = mPlane == SpatialPlane.XZ
                ? Mathf.FloorToInt(position.z * mInvCellSize)
                : Mathf.FloorToInt(position.y * mInvCellSize);
            return ((long)cellA << 32) | (uint)cellB;
        }
        
        /// <summary>
        /// 快速哈希计算（内联优化，避免 Mathf 调用）
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private long ComputeHashFast(Vector3 position)
        {
            float fa = position.x * mInvCellSize;
            float fb = mPlane == SpatialPlane.XZ ? position.z * mInvCellSize : position.y * mInvCellSize;
            int cellA = fa >= 0 ? (int)fa : (int)fa - 1;
            int cellB = fb >= 0 ? (int)fb : (int)fb - 1;
            return ((long)cellA << 32) | (uint)cellB;
        }

        internal static long ComputeHash(int cellA, int cellB) => ((long)cellA << 32) | (uint)cellB;

        private List<T> GetOrCreateCell(long hash)
        {
            if (!mCells.TryGetValue(hash, out var cell))
            {
                cell = RentList();
                mCells[hash] = cell;
            }
            return cell;
        }

        private List<T> RentList() => mListPool.Count > 0 ? mListPool.Pop() : new List<T>(4);
        private void RecycleList(List<T> list) { list.Clear(); mListPool.Push(list); }

        internal (int a, int b) GetCellCoord(Vector3 position)
        {
            int a = Mathf.FloorToInt(position.x * mInvCellSize);
            int b = mPlane == SpatialPlane.XZ
                ? Mathf.FloorToInt(position.z * mInvCellSize)
                : Mathf.FloorToInt(position.y * mInvCellSize);
            return (a, b);
        }


        /// <summary>
        /// 球形范围查询（零 GC）
        /// </summary>
        /// <param name="center">查询中心点</param>
        /// <param name="radius">查询半径</param>
        /// <param name="results">结果输出列表（调用前应清空）</param>
        public void QueryRadius(Vector3 center, float radius, List<T> results)
        {
            if (mCount == 0) return;

            float radiusSq = radius * radius;
            float cx = center.x, cy = center.y, cz = center.z;
            float centerB = mPlane == SpatialPlane.XZ ? cz : cy;
            
            // 快速计算格子范围（避免 Mathf 调用）
            float minA = (cx - radius) * mInvCellSize;
            float maxA = (cx + radius) * mInvCellSize;
            float minB = (centerB - radius) * mInvCellSize;
            float maxB = (centerB + radius) * mInvCellSize;
            
            int minCellA = minA >= 0 ? (int)minA : (int)minA - 1;
            int maxCellA = maxA >= 0 ? (int)maxA : (int)maxA - 1;
            int minCellB = minB >= 0 ? (int)minB : (int)minB - 1;
            int maxCellB = maxB >= 0 ? (int)maxB : (int)maxB - 1;

            for (int a = minCellA; a <= maxCellA; a++)
            {
                for (int b = minCellB; b <= maxCellB; b++)
                {
                    var hash = ComputeHash(a, b);
                    if (!mCells.TryGetValue(hash, out var cell)) continue;

                    for (int i = 0; i < cell.Count; i++)
                    {
                        var entity = cell[i];
                        var pos = entity.Position;
                        float dx = pos.x - cx;
                        float dy = pos.y - cy;
                        float dz = pos.z - cz;
                        if (dx * dx + dy * dy + dz * dz <= radiusSq) results.Add(entity);
                    }
                }
            }
        }

        /// <summary>
        /// AABB 范围查询（零 GC）
        /// </summary>
        /// <param name="bounds">查询边界</param>
        /// <param name="results">结果输出列表（调用前应清空）</param>
        public void QueryBounds(Bounds bounds, List<T> results)
        {
            if (mCount == 0) return;

            var min = bounds.min;
            var max = bounds.max;
            int minCellA = Mathf.FloorToInt(min.x * mInvCellSize);
            int maxCellA = Mathf.FloorToInt(max.x * mInvCellSize);
            
            int minCellB, maxCellB;
            if (mPlane == SpatialPlane.XZ)
            {
                minCellB = Mathf.FloorToInt(min.z * mInvCellSize);
                maxCellB = Mathf.FloorToInt(max.z * mInvCellSize);
            }
            else
            {
                minCellB = Mathf.FloorToInt(min.y * mInvCellSize);
                maxCellB = Mathf.FloorToInt(max.y * mInvCellSize);
            }

            for (int a = minCellA; a <= maxCellA; a++)
            {
                for (int b = minCellB; b <= maxCellB; b++)
                {
                    var hash = ComputeHash(a, b);
                    if (!mCells.TryGetValue(hash, out var cell)) continue;

                    for (int i = 0; i < cell.Count; i++)
                    {
                        var entity = cell[i];
                        var pos = entity.Position;
                        if (pos.x >= min.x && pos.x <= max.x &&
                            pos.y >= min.y && pos.y <= max.y &&
                            pos.z >= min.z && pos.z <= max.z)
                            results.Add(entity);
                    }
                }
            }
        }


        /// <summary>
        /// 最近邻查询
        /// </summary>
        /// <param name="position">查询位置</param>
        /// <param name="maxDistance">最大搜索距离，默认无限制</param>
        /// <param name="filter">可选过滤条件（使用 static lambda 避免 GC）</param>
        /// <returns>最近的实体，无结果返回 default(T)</returns>
        public T QueryNearest(Vector3 position, float maxDistance = float.MaxValue, Func<T, bool> filter = null)
        {
            if (mCount == 0) return default;

            if (maxDistance < 0f) maxDistance = 0f;

            if (maxDistance == float.MaxValue || float.IsInfinity(maxDistance))
            {
                T nearestUnbounded = default;
                float nearestDistSqUnbounded = float.MaxValue;
                bool foundUnbounded = false;

                foreach (var entity in mEntities.Values)
                {
                    if (filter != null && !filter(entity)) continue;

                    float distSq = (entity.Position - position).sqrMagnitude;
                    if (distSq < nearestDistSqUnbounded)
                    {
                        nearestDistSqUnbounded = distSq;
                        nearestUnbounded = entity;
                        foundUnbounded = true;
                    }
                }

                return foundUnbounded ? nearestUnbounded : default;
            }

            T nearest = default;
            float nearestDistSq = maxDistance * maxDistance;
            bool found = false;

            int searchRadius = Mathf.CeilToInt(maxDistance * mInvCellSize);
            int centerA = Mathf.FloorToInt(position.x * mInvCellSize);
            float posB = mPlane == SpatialPlane.XZ ? position.z : position.y;
            int centerB = Mathf.FloorToInt(posB * mInvCellSize);

            int minA = centerA - searchRadius;
            int maxA = centerA + searchRadius;
            int minB = centerB - searchRadius;
            int maxB = centerB + searchRadius;

            foreach (var kvp in mCells)
            {
                long hash = kvp.Key;
                var cell = kvp.Value;
                if (cell == null || cell.Count == 0) continue;

                int cellA = (int)(hash >> 32);
                int cellB = (int)(hash & 0xffffffff);

                if (cellA < minA || cellA > maxA || cellB < minB || cellB > maxB) continue;

                float cellMinA = cellA * mCellSize;
                float cellMaxA = cellMinA + mCellSize;
                float cellMinB = cellB * mCellSize;
                float cellMaxB = cellMinB + mCellSize;

                float da = position.x < cellMinA ? cellMinA - position.x : (position.x > cellMaxA ? position.x - cellMaxA : 0f);
                float db = posB < cellMinB ? cellMinB - posB : (posB > cellMaxB ? posB - cellMaxB : 0f);
                float minDistSq2D = da * da + db * db;
                if (minDistSq2D > nearestDistSq) continue;

                for (int i = 0; i < cell.Count; i++)
                {
                    var entity = cell[i];
                    if (filter != null && !filter(entity)) continue;

                    float distSq = (entity.Position - position).sqrMagnitude;
                    if (distSq <= nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearest = entity;
                        found = true;

                        if (nearestDistSq <= 0f) return nearest;
                    }
                }
            }

            return found ? nearest : default;
        }
    }
}
