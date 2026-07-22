namespace YokiFrame
{
    /// <summary>
    /// 定义可由 ActionKitScheduler 推进的最小动作生命周期。
    /// </summary>
    public interface IAction
    {
        /// <summary>获取当前执行租约的非零运行 ID。</summary>
        ulong ActionID { get; }

        /// <summary>获取或设置当前公开生命周期状态。</summary>
        ActionStatus ActionState { get; set; }

        /// <summary>获取或设置当前 Action 是否暂停。</summary>
        bool Paused { get; set; }

        /// <summary>获取当前执行租约是否已完成释放。</summary>
        bool Deinited { get; }

        /// <summary>在每次根启动或 Repeat 新一轮前重置运行状态。</summary>
        void OnInit();

        /// <summary>在正常完成、取消、故障或宿主重置时释放业务引用。</summary>
        void OnDeinit();

        /// <summary>首次推进当前执行轮次时调用。</summary>
        void OnStart();

        /// <summary>
        /// 使用当前 controller 选择的时间源推进动作。
        /// </summary>
        /// <param name="dt">本次推进秒数。</param>
        void OnExecute(float dt);

        /// <summary>仅在正常完成时调用一次。</summary>
        void OnFinish();

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 创建按需诊断使用的简短文本；自定义 Action 可以直接使用类型名默认实现。
        /// </summary>
        /// <returns>当前 Action 的诊断摘要。</returns>
        string GetDebugInfo() => GetType().Name;
#endif
    }
}
