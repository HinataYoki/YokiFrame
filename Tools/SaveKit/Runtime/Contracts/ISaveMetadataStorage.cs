namespace YokiFrame
{
    /// <summary>
    /// 为只读诊断提供容器头读取能力；实现不得读取、解析或复制模块 payload。
    /// </summary>
    public interface ISaveMetadataStorage
    {
        /// <summary>
        /// 尝试读取并验证目标的容器头；目标缺失、损坏或无法读取时返回 false。
        /// </summary>
        /// <param name="target">待读取的槽位或 Global 目标。</param>
        /// <param name="meta">成功时返回已验证的头部元数据。</param>
        /// <returns>头部可用且目标匹配时返回 true。</returns>
        bool TryReadMetadata(SaveTarget target, out SaveMeta meta);
    }
}
