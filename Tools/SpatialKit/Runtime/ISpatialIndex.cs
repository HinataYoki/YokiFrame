using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 定义 HashGrid、Quadtree 和 Octree 的统一空间索引操作。
    /// 查询方法把结果追加到调用者提供的列表，调用者负责在复用前清空列表。
    /// </summary>
    public interface ISpatialIndex<T> where T : ISpatialEntity
    {
        /// <summary>获取当前索引中的实体数量。</summary>
        int Count { get; }

        /// <summary>插入实体；同一索引内已有相同 ID 时替换旧实体。</summary>
        /// <param name="entity">待插入实体。</param>
        void Insert(T entity);

        /// <summary>按实体 ID 移除实体。</summary>
        /// <param name="entity">包含目标 ID 的实体；位置字段不会用于确定旧位置。</param>
        /// <returns>成功移除时返回 true。</returns>
        bool Remove(T entity);

        /// <summary>更新实体位置；实体不存在时按插入处理。</summary>
        /// <param name="entity">已更新位置的实体。</param>
        void Update(T entity);

        /// <summary>按顺序更新多个实体，结果等价于逐个调用 Update。</summary>
        /// <param name="entities">待更新实体列表。</param>
        void UpdateBatch(IReadOnlyList<T> entities);

        /// <summary>
        /// 查询半径内实体。HashGrid/Quadtree 使用构造时选择的二维投影平面，Octree 使用完整三维距离。
        /// </summary>
        /// <param name="center">查询中心。</param>
        /// <param name="radius">非负查询半径。</param>
        /// <param name="results">接收结果的列表。</param>
        void QueryRadius(YokiVector3 center, float radius, List<T> results);

        /// <summary>查询完整三维包围盒内的实体。</summary>
        /// <param name="bounds">查询包围盒。</param>
        /// <param name="results">接收结果的列表。</param>
        void QueryBounds(YokiBounds bounds, List<T> results);

        /// <summary>查询最近实体，距离度量遵循索引自身的二维或三维语义。</summary>
        /// <param name="position">查询位置。</param>
        /// <param name="maxDistance">最大距离；无穷大表示不限制距离。</param>
        /// <param name="filter">可选过滤器。</param>
        /// <returns>最近实体；没有符合条件的实体时返回 default。</returns>
        T QueryNearest(YokiVector3 position, float maxDistance = float.MaxValue, Func<T, bool> filter = null);

        /// <summary>清空全部实体并恢复索引的初始分区状态。</summary>
        void Clear();
    }
}
