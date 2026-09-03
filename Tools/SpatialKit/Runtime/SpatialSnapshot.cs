using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>描述一次二维投影半径查询，供 Snapshot 批量查询使用。</summary>
    public readonly struct SpatialRadiusQuery : IEquatable<SpatialRadiusQuery>
    {
        /// <summary>创建半径查询描述。</summary>
        /// <param name="center">查询中心。</param>
        /// <param name="radius">非负查询半径。</param>
        public SpatialRadiusQuery(YokiVector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        /// <summary>获取查询中心。</summary>
        public YokiVector3 Center { get; }

        /// <summary>获取查询半径。</summary>
        public float Radius { get; }

        /// <inheritdoc />
        public bool Equals(SpatialRadiusQuery other) => Center == other.Center && Radius == other.Radius;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is SpatialRadiusQuery other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Center, Radius);

        /// <summary>比较两个半径查询是否相等。</summary>
        public static bool operator ==(SpatialRadiusQuery left, SpatialRadiusQuery right) => left.Equals(right);

        /// <summary>比较两个半径查询是否不等。</summary>
        public static bool operator !=(SpatialRadiusQuery left, SpatialRadiusQuery right) => !left.Equals(right);
    }

    /// <summary>
    /// 描述一个索引的只读空间快照。快照保存捕获时的位置数组，适合跨线程批量查询。
    /// </summary>
    public sealed class SpatialIndexSnapshot<T> where T : ISpatialEntity
    {
        private readonly T[] mEntities;
        private readonly YokiVector3[] mPositions;
        private readonly SpatialPlane mPlane;
        private readonly bool mUsesThreeDimensionalDistance;

        /// <summary>创建只读空间快照并复制实体位置。</summary>
        /// <param name="entities">捕获时的实体引用数组。</param>
        /// <param name="plane">二维投影平面。</param>
        /// <param name="usesThreeDimensionalDistance">是否使用完整三维距离。</param>
        /// <param name="sourceVersion">创建快照时的 SpatialKit 状态版本。</param>
        internal SpatialIndexSnapshot(
            T[] entities,
            SpatialPlane plane,
            bool usesThreeDimensionalDistance,
            long sourceVersion)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            mEntities = entities;
            mPositions = new YokiVector3[entities.Length];
            for (int index = 0; index < entities.Length; index++)
            {
                SpatialMath.ValidatePosition(entities[index].Position, nameof(entities));
                mPositions[index] = entities[index].Position;
            }

            mPlane = plane;
            mUsesThreeDimensionalDistance = usesThreeDimensionalDistance;
            SourceVersion = sourceVersion;
        }

        /// <summary>获取快照中的实体数量。</summary>
        public int Count { get { return mEntities.Length; } }

        /// <summary>获取捕获时使用的投影平面。</summary>
        public SpatialPlane Plane { get { return mPlane; } }

        /// <summary>获取快照创建时的 SpatialKit 状态版本。</summary>
        public long SourceVersion { get; }

        /// <summary>查询快照中距离中心以内的实体。</summary>
        /// <param name="center">查询中心。</param>
        /// <param name="radius">非负查询半径。</param>
        /// <param name="results">接收结果的列表。</param>
        public void QueryRadius(YokiVector3 center, float radius, List<T> results)
        {
            ValidateRadiusQuery(center, radius, results);
            float radiusSquared = radius * radius;
            for (int index = 0; index < mPositions.Length; index++)
            {
                if (GetDistanceSquared(mPositions[index], center) <= radiusSquared)
                {
                    results.Add(mEntities[index]);
                }
            }
        }

        /// <summary>查询快照中位于三维包围盒内的实体。</summary>
        /// <param name="bounds">查询包围盒。</param>
        /// <param name="results">接收结果的列表。</param>
        public void QueryBounds(YokiBounds bounds, List<T> results)
        {
            SpatialMath.ValidateBounds(bounds, nameof(bounds));
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            for (int index = 0; index < mPositions.Length; index++)
            {
                if (bounds.Contains(mPositions[index]))
                {
                    results.Add(mEntities[index]);
                }
            }
        }

        /// <summary>查询快照中的最近实体。</summary>
        /// <param name="position">查询位置。</param>
        /// <param name="maxDistance">最大距离；无穷大表示不限制距离。</param>
        /// <param name="filter">可选实体过滤器。</param>
        /// <returns>最近实体；没有符合条件的实体时返回 default。</returns>
        public T QueryNearest(YokiVector3 position, float maxDistance = float.MaxValue, Func<T, bool> filter = null)
        {
            SpatialMath.ValidatePosition(position, nameof(position));
            ValidateDistance(maxDistance);
            float nearestDistanceSquared = SpatialMath.IsUnboundedDistance(maxDistance)
                ? float.MaxValue
                : maxDistance * maxDistance;
            T nearest = default(T);
            bool found = false;
            for (int index = 0; index < mPositions.Length; index++)
            {
                T entity = mEntities[index];
                if (filter != null && !filter(entity))
                {
                    continue;
                }

                float distanceSquared = GetDistanceSquared(mPositions[index], position);
                if (distanceSquared <= nearestDistanceSquared)
                {
                    nearest = entity;
                    nearestDistanceSquared = distanceSquared;
                    found = true;
                }
            }

            return found ? nearest : default(T);
        }

        /// <summary>按输入顺序执行多个半径查询，结果列表按查询索引对应。</summary>
        /// <param name="queries">半径查询描述列表。</param>
        /// <param name="results">数量必须与 queries 相同且每项非空。</param>
        public void QueryRadiusBatch(IReadOnlyList<SpatialRadiusQuery> queries, IList<List<T>> results)
        {
            ValidateBatch(queries, results);
            for (int index = 0; index < queries.Count; index++)
            {
                SpatialRadiusQuery query = queries[index];
                QueryRadius(query.Center, query.Radius, results[index]);
            }
        }

        /// <summary>并行执行多个互相独立的半径查询。</summary>
        /// <param name="queries">半径查询描述列表。</param>
        /// <param name="results">数量必须与 queries 相同且每项非空；每项只能由本次调用写入。</param>
        public void QueryRadiusBatchParallel(IReadOnlyList<SpatialRadiusQuery> queries, IList<List<T>> results)
        {
            ValidateParallelBatch(queries, results);
            Parallel.For(0, queries.Count, index =>
            {
                SpatialRadiusQuery query = queries[index];
                QueryRadius(query.Center, query.Radius, results[index]);
            });
        }

        /// <summary>按输入顺序执行多个包围盒查询。</summary>
        /// <param name="queries">三维包围盒列表。</param>
        /// <param name="results">数量必须与 queries 相同且每项非空。</param>
        public void QueryBoundsBatch(IReadOnlyList<YokiBounds> queries, IList<List<T>> results)
        {
            ValidateBatch(queries, results);
            for (int index = 0; index < queries.Count; index++)
            {
                QueryBounds(queries[index], results[index]);
            }
        }

        /// <summary>并行执行多个互相独立的包围盒查询。</summary>
        /// <param name="queries">三维包围盒列表。</param>
        /// <param name="results">数量必须与 queries 相同且每项非空；每项只能由本次调用写入。</param>
        public void QueryBoundsBatchParallel(IReadOnlyList<YokiBounds> queries, IList<List<T>> results)
        {
            ValidateParallelBatch(queries, results);
            Parallel.For(0, queries.Count, index => QueryBounds(queries[index], results[index]));
        }

        /// <summary>按快照的二维或三维距离语义计算距离平方。</summary>
        private float GetDistanceSquared(YokiVector3 position, YokiVector3 center)
        {
            return mUsesThreeDimensionalDistance
                ? (position - center).SqrMagnitude
                : SpatialMath.GetProjectedDistanceSquared(position, center, mPlane);
        }

        /// <summary>验证半径查询及结果容器。</summary>
        private static void ValidateRadiusQuery(YokiVector3 center, float radius, List<T> results)
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

        /// <summary>验证最近邻距离。</summary>
        private static void ValidateDistance(float distance)
        {
            if (float.IsNaN(distance) || distance < 0f || float.IsNegativeInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), "Maximum distance must be non-negative.");
            }
        }

        /// <summary>验证批量查询输入和一一对应的结果列表。</summary>
        private static void ValidateBatch<TQuery>(IReadOnlyList<TQuery> queries, IList<List<T>> results)
        {
            if (queries == null)
            {
                throw new ArgumentNullException(nameof(queries));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (results.Count != queries.Count)
            {
                throw new ArgumentException("The result count must match the query count.", nameof(results));
            }

            for (int index = 0; index < results.Count; index++)
            {
                if (results[index] == null)
                {
                    throw new ArgumentException("Result lists must not contain null entries.", nameof(results));
                }
            }
        }

        /// <summary>验证并行批量查询的结果列表引用互不重复，避免多个线程同时写入同一 List。</summary>
        private static void ValidateParallelBatch<TQuery>(IReadOnlyList<TQuery> queries, IList<List<T>> results)
        {
            ValidateBatch(queries, results);
            HashSet<List<T>> uniqueResults = new();
            for (int index = 0; index < results.Count; index++)
            {
                if (!uniqueResults.Add(results[index]))
                {
                    throw new ArgumentException("Parallel result lists must contain unique instances.", nameof(results));
                }
            }
        }
    }

    /// <summary>声明索引可以创建线程安全的只读快照。</summary>
    public interface ISpatialSnapshotProvider<T> where T : ISpatialEntity
    {
        /// <summary>捕获当前索引状态；调用期间不得并发修改索引。</summary>
        /// <returns>包含位置副本的只读空间快照。</returns>
        SpatialIndexSnapshot<T> CreateSnapshot();
    }

    /// <summary>为统一索引接口提供只读快照创建入口。</summary>
    public static class SpatialIndexSnapshotExtensions
    {
        /// <summary>从支持快照的索引创建只读快照。</summary>
        /// <typeparam name="T">空间实体类型。</typeparam>
        /// <param name="index">待捕获索引。</param>
        /// <returns>只读空间快照。</returns>
        public static SpatialIndexSnapshot<T> CreateSnapshot<T>(this ISpatialIndex<T> index)
            where T : ISpatialEntity
        {
            if (index == null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            if (index is ISpatialSnapshotProvider<T> provider)
            {
                return provider.CreateSnapshot();
            }

            throw new NotSupportedException("The spatial index does not provide snapshots.");
        }
    }
}
