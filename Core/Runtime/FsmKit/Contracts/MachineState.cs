namespace YokiFrame
{
    /// <summary>
    /// 表示状态机或并行子状态当前所处的生命周期阶段。
    /// </summary>
    public enum MachineState
    {
        /// <summary>状态机已经结束，当前不转发 tick 或消息。</summary>
        End = 0,

        /// <summary>状态机已经暂停，保留当前选择但不转发 tick 或消息。</summary>
        Suspend = 1,

        /// <summary>状态机正在运行，并向活动状态转发 tick 与消息。</summary>
        Running = 2
    }
}
