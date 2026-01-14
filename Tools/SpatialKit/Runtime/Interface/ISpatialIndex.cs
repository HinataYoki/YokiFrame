using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 空间索引统一接口
    /// </summary>
    /// <typeparam name="T">实体类型，必须实现 ISpatialEntity</typeparam>
    /// <remarks>
    /// 所有查询方法将结果写入外部 List 参数以避免 GC 分配。
    /// 调用查询方法前应清空结果列表。
    /// </remarks>
    public interface ISpatialIndex<T> where T : ISpatialEntity
    {
        /// <summary>
        /// 当前索引中的实体数量
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 插入实体到索引
        /// </summary>
        /// <param name="entity">要插入的实体</param>
        void Insert(T entity);

        /// <summary>
        /// 从索引中移除实体
        /// </summary>
        /// <param name="entity">要移除的实体</param>
        /// <returns>移除成功返回 true，实体不存在返回 false</returns>
        bool Remove(T entity);

        /// <summary>
        /// 更新实体位置（实体移动后调用）
        /// </summary>
        /// <param name="entity">位置已变化的实体</param>
        void Update(T entity);
        
        /// <summary>
        /// 批量更新实体位置（性能优化，减少重复查找开销）
        /// </summary>
        /// <param name="entities">位置已变化的实体列表</param>
        void UpdateBatch(IReadOnlyList<T> entities);

        /// <summary>
        /// 球形/圆形范围查询
        /// </summary>
        /// <param name="center">查询中心点</param>
        /// <param name="radius">查询半径</param>
        /// <param name="results">结果输出列表（调用前应清空）</param>
        void QueryRadius(Vector3 center, float radius, List<T> results);

        /// <summary>
        /// 矩形/立方体范围查询
        /// </summary>
        /// <param name="bounds">查询边界</param>
        /// <param name="results">结果输出列表（调用前应清空）</param>
        void QueryBounds(Bounds bounds, List<T> results);

        /// <summary>
        /// 最近邻查询
        /// </summary>
        /// <param name="position">查询位置</param>
        /// <param name="maxDistance">最大搜索距离，默认无限制</param>
        /// <param name="filter">可选过滤条件委托</param>
        /// <returns>最近的实体，无结果返回 default</returns>
        T QueryNearest(Vector3 position, float maxDistance = float.MaxValue, Func<T, bool> filter = null);

        /// <summary>
        /// 清空索引中的所有实体
        /// </summary>
        void Clear();
    }
}
