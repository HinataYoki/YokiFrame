namespace YokiFrame
{
    /// <summary>
    /// 表示 Action 的公开生命周期阶段；取消和故障由 controller 与调度诊断单独表达。
    /// </summary>
    public enum ActionStatus
    {
        /// <summary>尚未开始执行。</summary>
        NotStart,

        /// <summary>正在执行。</summary>
        Started,

        /// <summary>已离开执行路径。</summary>
        Finished
    }
}
