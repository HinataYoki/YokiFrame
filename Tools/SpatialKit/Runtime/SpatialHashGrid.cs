using System;
using System.Collections.Generic;
using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// 固定网格空间哈希索引，适合实体分布较均匀的场景。
    /// </summary>
    public class SpatialHashGrid<T> : ISpatialIndex<T>, ISpatialIndexDiagnostics where T : ISpatialEntity
    {
        private const float MIN_CELL_SIZE = 0f;
        private const int INITIAL_CELL_CAPACITY = 4;

        private readonly string mDiagnosticsId;
        private readonly string mEntityTypeName;
        private readonly string mCreatedAtUtc;
        private readonly float mCellSize;
        private readonly float mInvCellSize;
        private readonly SpatialPlane mPlane;
        private readonly Dictionary<long, List<T>> mCells = new();
        private readonly Dictionary<int, long> mEntityToCell = new();
        private readonly Dictionary<int, T> mEntities = new();
        private readonly Stack<List<T>> mListPool = new();
        private int mCount;

        /// <summary>
        /// 创建固定网格空间哈希索引。
        /// </summary>
        /// <param name="cellSize">网格尺寸。</param>
        /// <param name="plane">二维投影平面。</param>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="cellSize"/> 不大于 0 时抛出。</exception>
        public SpatialHashGrid(float cellSize, SpatialPlane plane = SpatialPlane.XZ)
        {
            if (cellSize <= MIN_CELL_SIZE)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "cellSize must be greater than 0.");
            }

            mDiagnosticsId = SpatialKitDiagnosticsRegistry.NextIndexId("hash-grid");
            mEntityTypeName = typeof(T).Name;
            mCreatedAtUtc = DateTime.UtcNow.ToString("O");
            mCellSize = cellSize;
            mInvCellSize = 1f / cellSize;
            mPlane = plane;
        }

        /// <inheritdoc />
        public int Count => mCount;

        /// <summary>
        /// 获取二维投影平面。
        /// </summary>
        public SpatialPlane Plane => mPlane;

        /// <inheritdoc />
        public string DiagnosticsId => mDiagnosticsId;

        /// <inheritdoc />
        public string IndexKind => "HashGrid";

        /// <inheritdoc />
        public string EntityTypeName => mEntityTypeName;

        /// <inheritdoc />
        public string PlaneName => mPlane.ToString();

        /// <inheritdoc />
        public float CellSize => mCellSize;

        /// <inheritdoc />
        public int MaxDepth => 0;

        /// <inheritdoc />
        public int MaxEntitiesPerNode => 0;

        /// <inheritdoc />
        public int PartitionCount => mCells.Count;

        /// <inheritdoc />
        public bool HasCellSize => true;

        /// <inheritdoc />
        public bool HasBounds2D => false;

        /// <inheritdoc />
        public bool HasBounds3D => false;

        /// <inheritdoc />
        public YokiRect Bounds2D => default;

        /// <inheritdoc />
        public YokiBounds Bounds3D => default;

        /// <inheritdoc />
        public string CreatedAtUtc => mCreatedAtUtc;

        /// <inheritdoc />
        public void Insert(T entity)
        {
            int id = entity.SpatialId;
            if (mEntityToCell.ContainsKey(id))
            {
                Remove(entity);
            }

            long hash = ComputeHash(entity.Position);
            var cell = GetOrCreateCell(hash);
            cell.Add(entity);
            mEntityToCell[id] = hash;
            mEntities[id] = entity;
            mCount++;
        }

        /// <inheritdoc />
        public bool Remove(T entity)
        {
            int id = entity.SpatialId;
            if (!mEntityToCell.TryGetValue(id, out long hash))
            {
                return false;
            }

            if (mCells.TryGetValue(hash, out var cell))
            {
                RemoveFromCell(cell, id);
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

        /// <inheritdoc />
        public void Update(T entity)
        {
            int id = entity.SpatialId;
            if (!mEntityToCell.TryGetValue(id, out long oldHash))
            {
                Insert(entity);
                return;
            }

            long newHash = ComputeHash(entity.Position);
            if (oldHash == newHash)
            {
                mEntities[id] = entity;
                if (mCells.TryGetValue(oldHash, out var currentCell))
                {
                    ReplaceInCell(currentCell, entity);
                }

                return;
            }

            if (mCells.TryGetValue(oldHash, out var oldCell))
            {
                RemoveFromCell(oldCell, id);
                if (oldCell.Count == 0)
                {
                    mCells.Remove(oldHash);
                    RecycleList(oldCell);
                }
            }

            var newCell = GetOrCreateCell(newHash);
            newCell.Add(entity);
            mEntityToCell[id] = newHash;
            mEntities[id] = entity;
        }

        /// <inheritdoc />
        public void UpdateBatch(IReadOnlyList<T> entities)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                Update(entities[i]);
            }
        }

        /// <inheritdoc />
        public void QueryRadius(YokiVector3 center, float radius, List<T> results)
        {
            if (mCount == 0 || radius < MIN_CELL_SIZE)
            {
                return;
            }

            float radiusSq = radius * radius;
            float centerB = mPlane == SpatialPlane.XZ ? center.Z : center.Y;
            int minCellA = SpatialMath.FloorToInt((center.X - radius) * mInvCellSize);
            int maxCellA = SpatialMath.FloorToInt((center.X + radius) * mInvCellSize);
            int minCellB = SpatialMath.FloorToInt((centerB - radius) * mInvCellSize);
            int maxCellB = SpatialMath.FloorToInt((centerB + radius) * mInvCellSize);

            for (int a = minCellA; a <= maxCellA; a++)
            {
                for (int b = minCellB; b <= maxCellB; b++)
                {
                    long hash = ComputeHash(a, b);
                    if (!mCells.TryGetValue(hash, out var cell))
                    {
                        continue;
                    }

                    for (int i = 0; i < cell.Count; i++)
                    {
                        var entity = cell[i];
                        if ((entity.Position - center).SqrMagnitude <= radiusSq)
                        {
                            results.Add(entity);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public void QueryBounds(YokiBounds bounds, List<T> results)
        {
            if (mCount == 0)
            {
                return;
            }

            var min = bounds.Min;
            var max = bounds.Max;
            int minCellA = SpatialMath.FloorToInt(min.X * mInvCellSize);
            int maxCellA = SpatialMath.FloorToInt(max.X * mInvCellSize);
            int minCellB = mPlane == SpatialPlane.XZ
                ? SpatialMath.FloorToInt(min.Z * mInvCellSize)
                : SpatialMath.FloorToInt(min.Y * mInvCellSize);
            int maxCellB = mPlane == SpatialPlane.XZ
                ? SpatialMath.FloorToInt(max.Z * mInvCellSize)
                : SpatialMath.FloorToInt(max.Y * mInvCellSize);

            for (int a = minCellA; a <= maxCellA; a++)
            {
                for (int b = minCellB; b <= maxCellB; b++)
                {
                    long hash = ComputeHash(a, b);
                    if (!mCells.TryGetValue(hash, out var cell))
                    {
                        continue;
                    }

                    for (int i = 0; i < cell.Count; i++)
                    {
                        var entity = cell[i];
                        if (bounds.Contains(entity.Position))
                        {
                            results.Add(entity);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public T QueryNearest(YokiVector3 position, float maxDistance = float.MaxValue, Func<T, bool> filter = null)
        {
            if (mCount == 0)
            {
                return default;
            }

            if (maxDistance < MIN_CELL_SIZE)
            {
                maxDistance = MIN_CELL_SIZE;
            }

            if (SpatialMath.IsUnboundedDistance(maxDistance))
            {
                return QueryNearestUnbounded(position, filter);
            }

            T nearest = default;
            float nearestDistSq = maxDistance * maxDistance;
            bool found = false;
            int searchRadius = SpatialMath.CeilToInt(maxDistance * mInvCellSize);
            int centerA = SpatialMath.FloorToInt(position.X * mInvCellSize);
            float posB = mPlane == SpatialPlane.XZ ? position.Z : position.Y;
            int centerB = SpatialMath.FloorToInt(posB * mInvCellSize);

            for (int a = centerA - searchRadius; a <= centerA + searchRadius; a++)
            {
                for (int b = centerB - searchRadius; b <= centerB + searchRadius; b++)
                {
                    long hash = ComputeHash(a, b);
                    if (!mCells.TryGetValue(hash, out var cell))
                    {
                        continue;
                    }

                    for (int i = 0; i < cell.Count; i++)
                    {
                        var entity = cell[i];
                        if (filter != null && !filter(entity))
                        {
                            continue;
                        }

                        float distSq = (entity.Position - position).SqrMagnitude;
                        if (distSq <= nearestDistSq)
                        {
                            nearest = entity;
                            nearestDistSq = distSq;
                            found = true;
                        }
                    }
                }
            }

            return found ? nearest : default;
        }

        /// <inheritdoc />
        public void Clear()
        {
            foreach (var cell in mCells.Values)
            {
                cell.Clear();
                RecycleList(cell);
            }

            mCells.Clear();
            mEntityToCell.Clear();
            mEntities.Clear();
            mCount = 0;
        }

        private T QueryNearestUnbounded(YokiVector3 position, Func<T, bool> filter)
        {
            T nearest = default;
            float nearestDistSq = float.MaxValue;
            bool found = false;
            foreach (var entity in mEntities.Values)
            {
                if (filter != null && !filter(entity))
                {
                    continue;
                }

                float distSq = (entity.Position - position).SqrMagnitude;
                if (distSq < nearestDistSq)
                {
                    nearest = entity;
                    nearestDistSq = distSq;
                    found = true;
                }
            }

            return found ? nearest : default;
        }

        private long ComputeHash(YokiVector3 position)
        {
            int cellA = SpatialMath.FloorToInt(position.X * mInvCellSize);
            int cellB = mPlane == SpatialPlane.XZ
                ? SpatialMath.FloorToInt(position.Z * mInvCellSize)
                : SpatialMath.FloorToInt(position.Y * mInvCellSize);
            return ComputeHash(cellA, cellB);
        }

        private static long ComputeHash(int cellA, int cellB)
        {
            return ((long)cellA << 32) | (uint)cellB;
        }

        private List<T> GetOrCreateCell(long hash)
        {
            if (!mCells.TryGetValue(hash, out var cell))
            {
                cell = mListPool.Count > 0 ? mListPool.Pop() : new List<T>(INITIAL_CELL_CAPACITY);
                mCells[hash] = cell;
            }

            return cell;
        }

        private void RecycleList(List<T> list)
        {
            list.Clear();
            mListPool.Push(list);
        }

        private static void RemoveFromCell(List<T> cell, int spatialId)
        {
            for (int i = cell.Count - 1; i >= 0; i--)
            {
                if (cell[i].SpatialId == spatialId)
                {
                    cell.RemoveAt(i);
                    return;
                }
            }
        }

        private static void ReplaceInCell(List<T> cell, T entity)
        {
            int spatialId = entity.SpatialId;
            for (int i = 0; i < cell.Count; i++)
            {
                if (cell[i].SpatialId == spatialId)
                {
                    cell[i] = entity;
                    return;
                }
            }
        }
    }
}
