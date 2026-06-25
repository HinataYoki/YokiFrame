using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// 可被空间索引管理的实体。
    /// </summary>
    public interface ISpatialEntity
    {
        /// <summary>
        /// 获取空间实体唯一编号。
        /// </summary>
        int SpatialId { get; }

        /// <summary>
        /// 获取空间实体位置。
        /// </summary>
        YokiVector3 Position { get; }
    }
}
