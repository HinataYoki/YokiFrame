namespace YokiFrame
{
    /// <summary>
    /// 描述一个可以被空间索引管理的实体。
    /// </summary>
    public interface ISpatialEntity
    {
        /// <summary>获取索引内稳定且唯一的实体编号。</summary>
        int SpatialId { get; }

        /// <summary>获取实体当前的世界位置。</summary>
        YokiVector3 Position { get; }
    }
}
