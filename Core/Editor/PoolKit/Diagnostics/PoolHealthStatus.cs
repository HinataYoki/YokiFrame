#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>
    /// 对象池使用率对应的健康状态。
    /// </summary>
    public enum PoolHealthStatus
    {
        /// <summary>
        /// 健康，当前使用率低于 50%。
        /// </summary>
        Healthy,

        /// <summary>
        /// 正常，当前使用率介于 50% 到 80%。
        /// </summary>
        Normal,

        /// <summary>
        /// 繁忙，当前使用率高于 80%。
        /// </summary>
        Busy,

        /// <summary>
        /// 警告，保留给后续频繁扩容等诊断策略。
        /// </summary>
        Warning
    }
}
#endif
