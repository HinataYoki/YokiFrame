namespace YokiFrame
{
    /// <summary>
    /// Buff 移除原因
    /// </summary>
    public enum BuffRemoveReason
    {
        /// <summary>
        /// 手动移除
        /// </summary>
        Manual,
        
        /// <summary>
        /// 时间到期
        /// </summary>
        Expired,
        
        /// <summary>
        /// 被排斥
        /// </summary>
        Excluded,
        
        /// <summary>
        /// 容器清空
        /// </summary>
        Cleared
    }
}
