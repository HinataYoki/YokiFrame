using System;
using System.Collections.Generic;
using System.Globalization;
namespace YokiFrame
{
    /// <summary>
    /// 使用整数网格和二维投影哈希管理动态实体的空间索引。
    /// </summary>
    public sealed partial class SpatialHashGrid<T> : ISpatialIndex<T>, ISpatialSnapshotProvider<T>
        where T : ISpatialEntity
    {
        private const float MIN_CELL_SIZE = 0f;
        private const int INITIAL_CELL_CAPACITY = 4;
#if UNITY_EDITOR || (GODOT && TOOLS)
        private readonly string mDiagnosticsId;
        private readonly string mEntityTypeName;
        private readonly string mCreatedAtUtc;
#endif
        private readonly float mCellSize;
        private readonly float mInverseCellSize;
        private readonly SpatialPlane mPlane;
        private readonly Dictionary<long, List<T>> mCells = new Dictionary<long, List<T>>();
        private readonly Dictionary<int, long> mEntityToCell = new Dictionary<int, long>();
        private readonly Dictionary<int, T> mEntities = new Dictionary<int, T>();
        private readonly Stack<List<T>> mListPool = new Stack<List<T>>();
        private int mCount;
        /// <summary>创建固定尺寸的二维投影哈希网格。</summary>
        /// <param name="cellSize">必须为有限正数的网格尺寸。</param>
        /// <param name="plane">用于分区和距离计算的二维投影平面。</param>
        public SpatialHashGrid(float cellSize, SpatialPlane plane = SpatialPlane.XZ)
        {
            if (!SpatialMath.IsFinite(cellSize) || cellSize <= MIN_CELL_SIZE)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be finite and greater than zero.");
            }
#if UNITY_EDITOR || (GODOT && TOOLS)
            mDiagnosticsId = SpatialKitDiagnosticsRegistry.NextIndexId("hash-grid");
            mEntityTypeName = typeof(T).FullName ?? typeof(T).Name;
            mCreatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
#endif
            mCellSize = cellSize;
            mInverseCellSize = 1f / cellSize;
            mPlane = plane;
#if UNITY_EDITOR || (GODOT && TOOLS)
            SpatialKitDiagnosticsRegistry.Register(this);
#endif
        }
        /// <summary>获取当前实体数量。</summary>
        public int Count { get { return mCount; } }
        /// <summary>获取二维投影平面。</summary>
        public SpatialPlane Plane { get { return mPlane; } }
#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>获取诊断编号。</summary>
        public string DiagnosticsId { get { return mDiagnosticsId; } }
        /// <summary>获取索引类型名称。</summary>
        public string IndexKind { get { return "HashGrid"; } }
        /// <summary>获取实体完整类型名称。</summary>
        public string EntityTypeName { get { return mEntityTypeName; } }
        /// <summary>获取投影平面名称。</summary>
        public string PlaneName { get { return mPlane.ToString(); } }
        /// <summary>获取网格尺寸。</summary>
        public float CellSize { get { return mCellSize; } }
        /// <summary>HashGrid 不使用树深度，固定返回零。</summary>
        public int MaxDepth { get { return 0; } }
        /// <summary>HashGrid 不使用节点容量，固定返回零。</summary>
        public int MaxEntitiesPerNode { get { return 0; } }
        /// <summary>获取当前非空网格分区数量。</summary>
        public int PartitionCount { get { return mCells.Count; } }
        /// <summary>表示当前诊断包含网格尺寸。</summary>
        public bool HasCellSize { get { return true; } }
        /// <summary>表示 HashGrid 没有固定二维边界。</summary>
        public bool HasBounds2D { get { return false; } }
        /// <summary>表示 HashGrid 没有固定三维边界。</summary>
        public bool HasBounds3D { get { return false; } }
        /// <summary>HashGrid 没有二维边界，返回默认值。</summary>
        public YokiRect Bounds2D { get { return default(YokiRect); } }
        /// <summary>HashGrid 没有三维边界，返回默认值。</summary>
        public YokiBounds Bounds3D { get { return default(YokiBounds); } }
        /// <summary>获取创建时间。</summary>
        public string CreatedAtUtc { get { return mCreatedAtUtc; } }
#endif
        /// <summary>插入实体；同 ID 实体会先按定位表移除。</summary>
        /// <param name="entity">待插入实体。</param>
        public void Insert(T entity)
        {
            ValidateEntity(entity);
            int id = entity.SpatialId;
            if (mEntityToCell.ContainsKey(id))
            {
                Remove(entity);
            }
            long hash = ComputeHash(entity.Position);
            List<T> cell = GetOrCreateCell(hash);
            cell.Add(entity);
            mEntityToCell[id] = hash;
            mEntities[id] = entity;
            mCount++;
#if UNITY_EDITOR || (GODOT && TOOLS)
            SpatialKitDiagnosticsRegistry.MarkChanged();
#endif
        }
        /// <summary>按 ID 从旧网格分区移除实体。</summary>
        /// <param name="entity">包含目标 ID 的实体。</param>
        /// <returns>找到并移除时返回 true。</returns>
        public bool Remove(T entity)
        {
            ValidateEntity(entity);
            int id = entity.SpatialId;
            if (!mEntityToCell.TryGetValue(id, out long hash))
            {
                return false;
            }
            if (mCells.TryGetValue(hash, out List<T> cell))
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
#if UNITY_EDITOR || (GODOT && TOOLS)
            SpatialKitDiagnosticsRegistry.MarkChanged();
#endif
            return true;
        }
        /// <summary>更新实体；定位表保证可变引用实体也能从旧分区移除。</summary>
        /// <param name="entity">已经更新位置的实体。</param>
        public void Update(T entity)
        {
            ValidateEntity(entity);
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
                if (!mCells.TryGetValue(oldHash, out List<T> currentCell)
                    || !ReplaceInCell(currentCell, entity))
                {
                    throw new InvalidOperationException("SpatialHashGrid lost the entity cell for ID " + id + ".");
                }
#if UNITY_EDITOR || (GODOT && TOOLS)
                SpatialKitDiagnosticsRegistry.MarkChanged();
#endif
                return;
            }
            RemoveFromExistingCell(id, oldHash);
            List<T> newCell = GetOrCreateCell(newHash);
            newCell.Add(entity);
            mEntityToCell[id] = newHash;
            mEntities[id] = entity;
#if UNITY_EDITOR || (GODOT && TOOLS)
            SpatialKitDiagnosticsRegistry.MarkChanged();
#endif
        }
        /// <summary>按输入顺序批量更新实体。</summary>
        /// <param name="entities">待更新实体列表。</param>
        public void UpdateBatch(IReadOnlyList<T> entities)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }
            for (int index = 0; index < entities.Count; index++)
            {
                Update(entities[index]);
            }
        }

        /// <summary>捕获当前网格的只读位置快照，供批量查询线程使用。</summary>
        /// <returns>保存实体引用和捕获时位置的快照。</returns>
        public SpatialIndexSnapshot<T> CreateSnapshot()
        {
            T[] entities = new T[mEntities.Count];
            int index = 0;
            foreach (T entity in mEntities.Values)
            {
                entities[index++] = entity;
            }

            return new SpatialIndexSnapshot<T>(
                entities,
                mPlane,
                false,
#if UNITY_EDITOR || (GODOT && TOOLS)
                SpatialKit.GetDiagnosticsVersion());
#else
                0L);
#endif
        }
        /// <summary>查询投影平面半径内的实体。</summary>
        /// <param name="center">查询中心。</param>
        /// <param name="radius">非负有限半径。</param>
        /// <param name="results">接收结果的列表。</param>
        public void QueryRadius(YokiVector3 center, float radius, List<T> results)
        {
            ValidateQuery(center, radius, results);
            if (mCount == 0)
            {
                return;
            }
            if (float.IsPositiveInfinity(radius))
            {
                AppendAllWithinProjectedRadius(center, radius, results);
                return;
            }
            float radiusSquared = radius * radius;
            float centerB = SpatialMath.GetPlaneCoordinate(center, mPlane);
            int minCellA = SpatialMath.FloorToInt((center.X - radius) * mInverseCellSize);
            int maxCellA = SpatialMath.FloorToInt((center.X + radius) * mInverseCellSize);
            int minCellB = SpatialMath.FloorToInt((centerB - radius) * mInverseCellSize);
            int maxCellB = SpatialMath.FloorToInt((centerB + radius) * mInverseCellSize);
            if (ShouldUseLinearScan(minCellA, maxCellA, minCellB, maxCellB))
            {
                AppendAllWithinProjectedRadius(center, radius, results);
                return;
            }
            for (long cellA = minCellA; cellA <= maxCellA; cellA++)
            {
                for (long cellB = minCellB; cellB <= maxCellB; cellB++)
                {
                    if (!mCells.TryGetValue(ComputeHash((int)cellA, (int)cellB), out List<T> cell))
                    {
                        continue;
                    }
                    AppendRadiusMatches(cell, center, radiusSquared, results);
                }
            }
        }
        /// <summary>查询完整三维包围盒内的实体。</summary>
        /// <param name="bounds">有限且正尺寸的查询包围盒。</param>
        /// <param name="results">接收结果的列表。</param>
        public void QueryBounds(YokiBounds bounds, List<T> results)
        {
            SpatialMath.ValidateBounds(bounds, nameof(bounds));
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }
            if (mCount == 0)
            {
                return;
            }
            YokiVector3 min = bounds.Min;
            YokiVector3 max = bounds.Max;
            int minCellA = SpatialMath.FloorToInt(min.X * mInverseCellSize);
            int maxCellA = SpatialMath.FloorToInt(max.X * mInverseCellSize);
            int minCellB = SpatialMath.FloorToInt(SpatialMath.GetPlaneCoordinate(min, mPlane) * mInverseCellSize);
            int maxCellB = SpatialMath.FloorToInt(SpatialMath.GetPlaneCoordinate(max, mPlane) * mInverseCellSize);
            if (ShouldUseLinearScan(minCellA, maxCellA, minCellB, maxCellB))
            {
                AppendAllWithinBounds(bounds, results);
                return;
            }
            for (long cellA = minCellA; cellA <= maxCellA; cellA++)
            {
                for (long cellB = minCellB; cellB <= maxCellB; cellB++)
                {
                    if (!mCells.TryGetValue(ComputeHash((int)cellA, (int)cellB), out List<T> cell))
                    {
                        continue;
                    }
                    for (int index = 0; index < cell.Count; index++)
                    {
                        if (bounds.Contains(cell[index].Position))
                        {
                            results.Add(cell[index]);
                        }
                    }
                }
            }
        }
        /// <summary>查询投影平面内的最近实体。</summary>
        /// <param name="position">查询位置。</param>
        /// <param name="maxDistance">最大投影距离。</param>
        /// <param name="filter">可选过滤器。</param>
        /// <returns>最近实体或 default。</returns>
        public T QueryNearest(YokiVector3 position, float maxDistance = float.MaxValue, Func<T, bool> filter = null)
        {
            SpatialMath.ValidatePosition(position, nameof(position));
            ValidateDistance(maxDistance);
            if (mCount == 0)
            {
                return default(T);
            }
            if (SpatialMath.IsUnboundedDistance(maxDistance))
            {
                return QueryNearestUnbounded(position, filter);
            }
            return QueryNearestWithinDistance(position, maxDistance, filter);
        }
        /// <summary>清空所有实体、分区和 ID 定位信息。</summary>
        public void Clear()
        {
            foreach (List<T> cell in mCells.Values)
            {
                RecycleList(cell);
            }
            mCells.Clear();
            mEntityToCell.Clear();
            mEntities.Clear();
            mCount = 0;
#if UNITY_EDITOR || (GODOT && TOOLS)
            SpatialKitDiagnosticsRegistry.MarkChanged();
#endif
        }
        /// <summary>验证实体、位置和结果列表的公共输入约束。</summary>
        private static void ValidateEntity(T entity)
        {
            if (ReferenceEquals(entity, null))
            {
                throw new ArgumentNullException(nameof(entity));
            }
            SpatialMath.ValidatePosition(entity.Position, nameof(entity));
        }
        /// <summary>验证半径查询输入。</summary>
        private static void ValidateQuery(YokiVector3 center, float radius, List<T> results)
        {
            SpatialMath.ValidatePosition(center, nameof(center));
            if (float.IsNaN(radius) || radius < 0f || float.IsNegativeInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be non-negative.");
            }
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }
        }
        /// <summary>验证最近邻距离允许有限值或正无穷。</summary>
        private static void ValidateDistance(float distance)
        {
            if (float.IsNaN(distance) || distance < 0f || float.IsNegativeInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), "Maximum distance must be non-negative.");
            }
        }
        /// <summary>把所有实体按投影距离过滤到结果列表。</summary>
        private void AppendAllWithinProjectedRadius(YokiVector3 center, float radius, List<T> results)
        {
            foreach (T entity in mEntities.Values)
            {
                if (SpatialMath.GetProjectedDistanceSquared(entity.Position, center, mPlane) <= radius * radius)
                {
                    results.Add(entity);
                }
            }
        }
        /// <summary>把一个网格 cell 中命中的实体追加到结果列表。</summary>
        private void AppendRadiusMatches(List<T> cell, YokiVector3 center, float radiusSquared, List<T> results)
        {
            for (int index = 0; index < cell.Count; index++)
            {
                T entity = cell[index];
                if (SpatialMath.GetProjectedDistanceSquared(entity.Position, center, mPlane) <= radiusSquared)
                {
                    results.Add(entity);
                }
            }
        }
        /// <summary>在线性扫描中查找无限距离最近实体。</summary>
        private T QueryNearestUnbounded(YokiVector3 position, Func<T, bool> filter)
        {
            return QueryNearestLinear(position, float.PositiveInfinity, filter);
        }
        /// <summary>根据实体位置计算二维网格哈希。</summary>
        private long ComputeHash(YokiVector3 position)
        {
            int cellA = SpatialMath.FloorToInt(position.X * mInverseCellSize);
            int cellB = SpatialMath.FloorToInt(SpatialMath.GetPlaneCoordinate(position, mPlane) * mInverseCellSize);
            return ComputeHash(cellA, cellB);
        }
        /// <summary>把两个有符号 cell 坐标无碰撞地编码为 long。</summary>
        private static long ComputeHash(int cellA, int cellB)
        {
            return ((long)cellA << 32) | (uint)cellB;
        }
        /// <summary>获取已存在或新建的 cell 列表。</summary>
        private List<T> GetOrCreateCell(long hash)
        {
            if (!mCells.TryGetValue(hash, out List<T> cell))
            {
                cell = mListPool.Count > 0 ? mListPool.Pop() : new List<T>(INITIAL_CELL_CAPACITY);
                mCells[hash] = cell;
            }
            return cell;
        }
        /// <summary>从指定旧 cell 移除实体并回收空列表。</summary>
        private void RemoveFromExistingCell(int id, long hash)
        {
            if (!mCells.TryGetValue(hash, out List<T> cell) || !RemoveFromCell(cell, id))
            {
                throw new InvalidOperationException("SpatialHashGrid lost the entity cell for ID " + id + ".");
            }
            if (cell.Count == 0)
            {
                mCells.Remove(hash);
                RecycleList(cell);
            }
        }
        /// <summary>清空并缓存不再使用的 cell 列表。</summary>
        private void RecycleList(List<T> list)
        {
            list.Clear();
            mListPool.Push(list);
        }
        /// <summary>按实体 ID 从 cell 移除一项。</summary>
        private static bool RemoveFromCell(List<T> cell, int spatialId)
        {
            for (int index = cell.Count - 1; index >= 0; index--)
            {
                if (cell[index].SpatialId == spatialId)
                {
                    cell.RemoveAt(index);
                    return true;
                }
            }
            return false;
        }
        /// <summary>在同一 cell 内替换实体引用或值。</summary>
        private static bool ReplaceInCell(List<T> cell, T entity)
        {
            int spatialId = entity.SpatialId;
            for (int index = 0; index < cell.Count; index++)
            {
                if (cell[index].SpatialId == spatialId)
                {
                    cell[index] = entity;
                    return true;
                }
            }
            return false;
        }
    }
}
