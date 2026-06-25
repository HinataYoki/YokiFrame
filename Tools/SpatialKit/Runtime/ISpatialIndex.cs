using System;
using System.Collections.Generic;
using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// 空间索引统一接口，查询结果写入调用方传入的列表。
    /// </summary>
    public interface ISpatialIndex<T> where T : ISpatialEntity
    {
        /// <summary>
        /// 获取当前索引中的实体数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 插入实体；如果同 ID 实体已存在则先移除旧实体。
        /// </summary>
        /// <param name="entity">空间实体。</param>
        void Insert(T entity);

        /// <summary>
        /// 移除实体。
        /// </summary>
        /// <param name="entity">空间实体。</param>
        /// <returns>成功移除时返回 true。</returns>
        bool Remove(T entity);

        /// <summary>
        /// 更新实体位置；实体不存在时插入。
        /// </summary>
        /// <param name="entity">空间实体。</param>
        void Update(T entity);

        /// <summary>
        /// 批量更新实体位置。
        /// </summary>
        /// <param name="entities">空间实体列表。</param>
        void UpdateBatch(IReadOnlyList<T> entities);

        /// <summary>
        /// 查询指定半径内的实体。
        /// </summary>
        /// <param name="center">查询中心。</param>
        /// <param name="radius">查询半径。</param>
        /// <param name="results">查询结果写入列表。</param>
        void QueryRadius(YokiVector3 center, float radius, List<T> results);

        /// <summary>
        /// 查询指定包围盒内的实体。
        /// </summary>
        /// <param name="bounds">查询包围盒。</param>
        /// <param name="results">查询结果写入列表。</param>
        void QueryBounds(YokiBounds bounds, List<T> results);

        /// <summary>
        /// 查询最近实体。
        /// </summary>
        /// <param name="position">查询位置。</param>
        /// <param name="maxDistance">最大查询距离。</param>
        /// <param name="filter">可选实体过滤器。</param>
        /// <returns>最近实体；未找到时返回默认值。</returns>
        T QueryNearest(YokiVector3 position, float maxDistance = float.MaxValue, Func<T, bool> filter = null);

        /// <summary>
        /// 清空索引。
        /// </summary>
        void Clear();
    }
}
