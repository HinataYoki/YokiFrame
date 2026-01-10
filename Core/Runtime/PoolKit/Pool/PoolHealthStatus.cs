namespace YokiFrame
{
    /// <summary>
    /// 对象池健康状态
    /// </summary>
    public enum PoolHealthStatus
    {
        /// <summary>
        /// 健康 - 使用率 &lt; 50%
        /// </summary>
        Healthy,
        
        /// <summary>
        /// 正常 - 使用率 50%-80%
        /// </summary>
        Normal,
        
        /// <summary>
        /// 繁忙 - 使用率 &gt; 80%
        /// </summary>
        Busy,
        
        /// <summary>
        /// 警告 - 频繁扩容
        /// </summary>
        Warning
    }
}
