namespace YokiFrame
{
    /// <summary>
    /// Action 的生命周期状态。
    /// </summary>
    public enum ActionStatus
    {
        /// <summary>
        /// 尚未开始执行。
        /// </summary>
        NotStart,

        /// <summary>
        /// 正在执行。
        /// </summary>
        Started,

        /// <summary>
        /// 已完成。
        /// </summary>
        Finished,
    }
}
