using System;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// 对外稳定的根动作 handle；实例不回池，因此旧引用不会控制后续执行租约。
    /// </summary>
    public sealed class ActionController : IActionController
    {
        private const int TERMINAL_NONE = 0;
        private const int TERMINAL_COMPLETED = 1;
        private const int TERMINAL_CANCELLED = 2;
        private const int TERMINAL_FAULTED = 3;
        private IAction mAction;
        private Action<IActionController> mFinish;
        private Exception mPendingFault;
        private ActionUpdateModes mUpdateMode;
        private bool mActionIdsRegistered;
#if UNITY_EDITOR || (GODOT && TOOLS)
        private bool mStackTraceRegistered;
#endif
        private int mCancelRequested;
        private int mTerminalState;

        /// <summary>
        /// 创建绑定根 Action 和正常完成回调的稳定 handle；仅由 Scheduler 调用。
        /// </summary>
        /// <param name="action">当前根 Action。</param>
        /// <param name="finish">仅在正常完成时调用的回调。</param>
        internal ActionController(IAction action, Action<IActionController> finish)
        {
            mAction = action ?? throw new ArgumentNullException(nameof(action));
            CurExecuteActionID = action.ActionID;
            mFinish = finish;
            mUpdateMode = ActionUpdateModes.ScaledDeltaTime;
        }

        /// <summary>获取 handle 创建时绑定的稳定 Action ID。</summary>
        public ulong CurExecuteActionID { get; }

        /// <summary>获取仍活动或正在完成回调中的根 Action；终结清理后返回 null。</summary>
        public IAction Action => mAction;

        /// <summary>获取或在 Scheduler 宿主线程设置当前根动作树使用的时间源。</summary>
        public ActionUpdateModes UpdateMode
        {
            get => mUpdateMode;
            set
            {
                ActionKitScheduler.EnsureHostThread();
                if (value != ActionUpdateModes.ScaledDeltaTime && value != ActionUpdateModes.UnscaledDeltaTime)
                    throw new ArgumentOutOfRangeException(nameof(value));
                if (mUpdateMode == value || IsCompleted || Volatile.Read(ref mPendingFault) != null) return;
                mUpdateMode = value;
                try
                {
                    ActionRuntime.SetUpdateMode(mAction, value);
                }
                catch (Exception exception)
                {
                    RequestFault(exception);
                    throw;
                }
#if UNITY_EDITOR || (GODOT && TOOLS)
                ActionKitScheduler.NotifyStateChanged();
#endif
            }
        }

        /// <summary>获取仅在正常完成时调用一次的 controller 回调。</summary>
        public Action<IActionController> Finish => mFinish;

        /// <summary>获取或在 Scheduler 宿主线程设置暂停状态；终结后的旧 handle 保持无操作。</summary>
        public bool Paused
        {
            get => mAction != null && mAction.Paused;
            set
            {
                ActionKitScheduler.EnsureHostThread();
                if (mAction == null || IsCompleted || Volatile.Read(ref mPendingFault) != null || mAction.Paused == value)
                    return;
                try
                {
                    ActionRuntime.SetPaused(mAction, value);
                }
                catch (Exception exception)
                {
                    RequestFault(exception);
                    throw;
                }
#if UNITY_EDITOR || (GODOT && TOOLS)
                ActionKitScheduler.NotifyStateChanged();
#endif
            }
        }

        /// <summary>获取当前 handle 是否已请求或完成取消。</summary>
        public bool IsCancelled
        {
            get
            {
                int terminalState = Volatile.Read(ref mTerminalState);
                return terminalState == TERMINAL_CANCELLED
                    || (terminalState == TERMINAL_NONE && Volatile.Read(ref mCancelRequested) != 0);
            }
        }

        /// <summary>获取当前 handle 是否已经离开调度器。</summary>
        public bool IsCompleted => Volatile.Read(ref mTerminalState) != TERMINAL_NONE;

        /// <summary>获取当前 handle 是否因生命周期异常结束。</summary>
        public bool IsFaulted => Volatile.Read(ref mTerminalState) == TERMINAL_FAULTED;

        /// <summary>获取调度器是否需要在当前 Tick 走取消终态。</summary>
        internal bool CancellationRequested => Volatile.Read(ref mTerminalState) == TERMINAL_NONE
            && Volatile.Read(ref mCancelRequested) != 0;

        /// <summary>获取控制钩子是否已经登记尚未终结的生命周期故障。</summary>
        internal bool HasPendingFault => Volatile.Read(ref mPendingFault) != null;

        /// <summary>取出控制钩子登记的首个故障，交由宿主 Tick 串行终结动作树。</summary>
        /// <param name="exception">待形成 Faulted 终态的原始异常。</param>
        /// <returns>存在尚未处理的控制故障时返回 true。</returns>
        internal bool TryTakePendingFault(out Exception exception)
        {
            if (Volatile.Read(ref mPendingFault) == null)
            {
                exception = null;
                return false;
            }

            exception = Interlocked.Exchange(ref mPendingFault, null);
            return exception != null;
        }

        /// <summary>标记当前动作树的全部 ID 已进入 Scheduler 活动集合。</summary>
        internal void MarkActionIdsRegistered() => mActionIdsRegistered = true;

        /// <summary>仅首次终结时清除活动 ID 登记标记。</summary>
        /// <returns>调用方需要从 Scheduler 集合移除当前树 ID 时返回 true。</returns>
        internal bool TryClearActionIdsRegistered()
        {
            if (!mActionIdsRegistered) return false;
            mActionIdsRegistered = false;
            return true;
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>标记当前 controller 已登记自己的 Start 堆栈。</summary>
        internal void MarkStackTraceRegistered() => mStackTraceRegistered = true;

        /// <summary>仅让实际登记堆栈的 controller 删除对应记录。</summary>
        /// <returns>需要删除当前 Action ID 堆栈时返回 true。</returns>
        internal bool TryClearStackTraceRegistered()
        {
            if (!mStackTraceRegistered) return false;
            mStackTraceRegistered = false;
            return true;
        }
#endif

        /// <summary>从任意线程请求取消当前租约；宿主线程可同步释放仍在准备队列中的动作，其余情况由 Tick 串行终结。</summary>
        public void Cancel()
        {
            if (IsCompleted) return;
#if UNITY_EDITOR || (GODOT && TOOLS)
            bool changed = Interlocked.Exchange(ref mCancelRequested, 1) == 0;
#else
            Interlocked.Exchange(ref mCancelRequested, 1);
#endif
            int terminalState = Volatile.Read(ref mTerminalState);
            if (terminalState != TERMINAL_NONE)
            {
                if (terminalState != TERMINAL_CANCELLED) Volatile.Write(ref mCancelRequested, 0);
                return;
            }
            if (ActionKitScheduler.TryFinalizePreparedCancellation(this)) return;
#if UNITY_EDITOR || (GODOT && TOOLS)
            if (changed)
                ActionKitScheduler.NotifyStateChanged();
#endif
        }

        /// <summary>把 handle 标记为正常完成，使完成回调内可以观察终态。</summary>
        internal void MarkCompleted()
        {
            Volatile.Write(ref mTerminalState, TERMINAL_COMPLETED);
            Volatile.Write(ref mCancelRequested, 0);
        }

        /// <summary>把 handle 标记为取消，并保持 IsCancelled 为 true。</summary>
        internal void MarkCancelled()
        {
            Volatile.Write(ref mCancelRequested, 1);
            Volatile.Write(ref mTerminalState, TERMINAL_CANCELLED);
        }

        /// <summary>把 handle 标记为故障。</summary>
        internal void MarkFaulted()
        {
            Volatile.Write(ref mTerminalState, TERMINAL_FAULTED);
            Volatile.Write(ref mCancelRequested, 0);
        }

        /// <summary>终结清理后释放根 Action 和完成委托强引用，避免旧 handle 延长业务对象生命周期。</summary>
        internal void DetachAction()
        {
            mAction = null;
            mFinish = null;
            mActionIdsRegistered = false;
#if UNITY_EDITOR || (GODOT && TOOLS)
            mStackTraceRegistered = false;
#endif
            Interlocked.Exchange(ref mPendingFault, null);
        }

        /// <summary>登记首个控制钩子异常；实际清理由 Scheduler 在下一宿主 Tick 完成。</summary>
        /// <param name="exception">OnPause、OnResume 或 OnUpdateModeChanged 抛出的异常。</param>
        private void RequestFault(Exception exception)
        {
#if UNITY_EDITOR || (GODOT && TOOLS)
            if (Interlocked.CompareExchange(ref mPendingFault, exception, null) == null)
                ActionKitScheduler.NotifyStateChanged();
#else
            Interlocked.CompareExchange(ref mPendingFault, exception, null);
#endif
        }
    }
}
