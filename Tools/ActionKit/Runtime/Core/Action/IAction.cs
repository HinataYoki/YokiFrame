namespace YokiFrame
{
    /// <summary>
    /// ActionKit 中所有可调度动作的基础契约。
    /// </summary>
    public interface IAction
    {
        /// <summary>
        /// 当前 Action 的唯一运行时编号。
        /// </summary>
        ulong ActionID { get; }

        /// <summary>
        /// 当前 Action 的生命周期状态。
        /// </summary>
        ActionStatus ActionState { get; set; }

        /// <summary>
        /// 当前 Action 是否暂停。
        /// </summary>
        bool Paused { get; set; }

        /// <summary>
        /// 当前 Action 是否已经释放运行时状态。
        /// </summary>
        bool Deinited { get; }

        /// <summary>
        /// 初始化 Action 运行时状态。
        /// </summary>
        void OnInit();

        /// <summary>
        /// 释放 Action 运行时状态并归还池。
        /// </summary>
        void OnDeinit();

        /// <summary>
        /// Action 首次执行时调用。
        /// </summary>
        void OnStart();

        /// <summary>
        /// Action 每次调度更新时调用。
        /// </summary>
        /// <param name="dt">本次更新的时间步长。</param>
        void OnExecute(float dt);

        /// <summary>
        /// Action 完成时调用。
        /// </summary>
        void OnFinish();

        /// <summary>
        /// 返回用于调试面板展示的简短信息。
        /// </summary>
        string GetDebugInfo() => GetType().Name;
    }
}
