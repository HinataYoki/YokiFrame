using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 可被空间索引的实体接口
    /// </summary>
    /// <remarks>
    /// 支持 struct 和 class 类型实现。
    /// 实现类需确保 SpatialId 在索引生命周期内唯一。
    /// </remarks>
    public interface ISpatialEntity
    {
        /// <summary>
        /// 唯一标识（用于快速查找和去重）
        /// </summary>
        int SpatialId { get; }

        /// <summary>
        /// 当前世界坐标位置
        /// </summary>
        Vector3 Position { get; }
    }
}
