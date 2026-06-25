namespace YokiFrame
{
    /// <summary>
    /// Action 抽象基类，处理公共状态和初始化逻辑。
    /// </summary>
    public abstract class ActionBase : IAction
    {
        /// <summary>
        /// 当前 Action 的唯一运行时编号。
        /// </summary>
        public ulong ActionID { get; protected set; }

        /// <summary>
        /// 当前 Action 的生命周期状态。
        /// </summary>
        public ActionStatus ActionState { get; set; }

        /// <summary>
        /// 当前 Action 是否暂停。
        /// </summary>
        public bool Paused { get; set; }

        /// <summary>
        /// 当前 Action 是否已经释放运行时状态。
        /// </summary>
        public bool Deinited { get; protected set; }

        /// <summary>
        /// 初始化 Action 运行时状态。
        /// </summary>
        public virtual void OnInit()
        {
            ActionState = ActionStatus.NotStart;
            Paused = false;
        }

        /// <summary>
        /// 释放 Action 运行时状态并归还池。
        /// </summary>
        public abstract void OnDeinit();

        /// <summary>
        /// Action 首次执行时调用。
        /// </summary>
        public virtual void OnStart() { }

        /// <summary>
        /// Action 每次调度更新时调用。
        /// </summary>
        /// <param name="dt">本次更新的时间步长。</param>
        public virtual void OnExecute(float dt) { }

        /// <summary>
        /// Action 完成时调用。
        /// </summary>
        public virtual void OnFinish() { }

        /// <summary>
        /// 返回用于调试面板展示的简短信息。
        /// </summary>
        public virtual string GetDebugInfo() => GetType().Name;
    }
}
