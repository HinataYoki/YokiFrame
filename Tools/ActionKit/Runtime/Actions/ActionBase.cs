namespace YokiFrame
{
    /// <summary>
    /// 为自定义与内置 Action 提供公共状态、运行 ID 和暂停扩展钩子。
    /// </summary>
    public abstract class ActionBase : IAction
    {
        private IAction mParent;
        private bool mActiveRoot;
        private bool mFinishInvoked;

        /// <summary>获取当前执行租约的非零运行 ID。</summary>
        public ulong ActionID { get; protected set; }

        /// <summary>获取或设置当前公开生命周期状态。</summary>
        public ActionStatus ActionState { get; set; }

        /// <summary>获取或设置当前 Action 是否暂停。</summary>
        public bool Paused { get; set; }

        /// <summary>获取当前执行租约是否已经释放。</summary>
        public bool Deinited { get; protected set; }

        /// <summary>获取当前执行轮次是否已经调用 OnInit。</summary>
        internal bool IsInitialized { get; private set; }

        /// <summary>
        /// 重置公共运行状态；派生类型应先调用 base 再重置自己的字段。
        /// </summary>
        public virtual void OnInit()
        {
            ActionState = ActionStatus.NotStart;
            Paused = false;
            IsInitialized = false;
            mFinishInvoked = false;
        }

        /// <summary>
        /// 释放派生类型持有的业务引用；调度器会在调用后统一标记已释放。
        /// </summary>
        public virtual void OnDeinit() { }

        /// <summary>首次推进当前执行轮次时调用。</summary>
        public virtual void OnStart() { }

        /// <summary>
        /// 推进当前动作；默认实现保持运行，派生类型应在完成时调用 Finish。
        /// </summary>
        /// <param name="dt">本次推进秒数。</param>
        public virtual void OnExecute(float dt) { }

        /// <summary>仅在正常完成时调用一次。</summary>
        public virtual void OnFinish() { }

        /// <summary>当 controller 从运行切换到暂停时调用。</summary>
        public virtual void OnPause() { }

        /// <summary>当 controller 从暂停恢复运行时调用。</summary>
        public virtual void OnResume() { }

        /// <summary>
        /// 当 controller 时间源变化时调用，供 DOTween 等外部驱动同步配置。
        /// </summary>
        /// <param name="updateMode">新的时间源。</param>
        public virtual void OnUpdateModeChanged(ActionUpdateModes updateMode) { }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 返回按需诊断摘要；默认使用类型名，只有调用方显式读取时才创建文本。
        /// </summary>
        /// <returns>当前 Action 类型名。</returns>
        public virtual string GetDebugInfo() => GetType().Name;
#endif

        /// <summary>
        /// 为首次启动的自定义 Action 分配 ID；内置池化 Action 会在 Allocate 时覆盖 ID。
        /// </summary>
        /// <param name="actionId">当前执行租约 ID。</param>
        internal void PrepareExecution(ulong actionId)
        {
            if (ActionID == 0)
            {
                ActionID = actionId;
            }

            ActionState = ActionStatus.NotStart;
            Paused = false;
            IsInitialized = false;
            Deinited = false;
            mFinishInvoked = false;
        }

        /// <summary>
        /// 在池借出时安装新的运行 ID，并清除上一个租约的生命周期标记。
        /// </summary>
        /// <param name="actionId">新的非零运行 ID。</param>
        internal void PreparePooled(ulong actionId)
        {
            ActionID = actionId;
            ActionState = ActionStatus.NotStart;
            Paused = false;
            Deinited = false;
            IsInitialized = false;
            mFinishInvoked = false;
        }

        /// <summary>由生命周期协调器在 OnInit 返回后确认初始化完成。</summary>
        internal void MarkInitialized() => IsInitialized = true;

        /// <summary>由生命周期协调器在 OnDeinit 后确认当前租约已释放。</summary>
        internal void MarkDeinited()
        {
            Deinited = true;
            IsInitialized = false;
        }

        /// <summary>获取当前 Action 是否已经属于父容器或活动根。</summary>
        internal bool HasOwnership => mParent != null || mActiveRoot;

        /// <summary>获取当前 Action 的直接父容器；根 Action 返回 null。</summary>
        internal IAction ParentAction => mParent;

        /// <summary>获取当前 Action 是否由活动 controller 持有。</summary>
        internal bool IsActiveRoot => mActiveRoot;

        /// <summary>记录唯一父容器；所有权冲突由 ActionOwnership 在调用前校验。</summary>
        /// <param name="parent">当前 Action 的直接父容器。</param>
        internal void ClaimParent(IAction parent) => mParent = parent;

        /// <summary>记录当前 Action 已成为活动根。</summary>
        internal void ClaimActiveRoot() => mActiveRoot = true;

        /// <summary>释放当前租约的父级或活动根身份。</summary>
        internal void ReleaseOwnership()
        {
            mParent = null;
            mActiveRoot = false;
        }

        /// <summary>
        /// 原子于宿主线程地记录正常完成钩子已经开始，阻止手动重复 Update 再次调用 OnFinish。
        /// </summary>
        /// <returns>本轮首次进入 OnFinish 时返回 true。</returns>
        internal bool TryBeginFinish()
        {
            if (mFinishInvoked) return false;
            mFinishInvoked = true;
            return true;
        }
    }
}
