#if UNITY_EDITOR
using System;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 防抖器 - 延迟执行，重复调用会重置计时器
    /// 适用于搜索输入、文件监控等场景
    /// </summary>
    public sealed class Debounce : IDisposable
    {
        private readonly float mDelay;
        private readonly VisualElement mScheduleHost;
        private IVisualElementScheduledItem mScheduledItem;
        private Action mPendingAction;
        private bool mIsDisposed;

        /// <summary>
        /// 创建防抖器
        /// </summary>
        /// <param name="delaySeconds">延迟时间（秒）</param>
        /// <param name="scheduleHost">用于调度的 VisualElement（通常是根元素）</param>
        public Debounce(float delaySeconds, VisualElement scheduleHost)
        {
            mDelay = delaySeconds;
            mScheduleHost = scheduleHost;
        }

        /// <summary>
        /// 执行动作（受防抖限制）
        /// 如果在延迟时间内再次调用，会重置计时器
        /// </summary>
        public void Execute(Action action)
        {
            if (mIsDisposed || action is null || mScheduleHost is null) return;

            // 取消之前的调度
            Cancel();

            mPendingAction = action;

            // 创建新的延迟调度
            mScheduledItem = mScheduleHost.schedule.Execute(() =>
            {
                if (mIsDisposed) return;
                mPendingAction?.Invoke();
                mPendingAction = null;
                mScheduledItem = null;
            }).StartingIn((long)(mDelay * 1000));
        }

        /// <summary>
        /// 取消待执行的动作
        /// </summary>
        public void Cancel()
        {
            mScheduledItem?.Pause();
            mScheduledItem = null;
            mPendingAction = null;
        }

        /// <summary>
        /// 立即执行待处理的动作（如果有）
        /// </summary>
        public void Flush()
        {
            if (mIsDisposed || mPendingAction is null) return;

            Cancel();
            var action = mPendingAction;
            mPendingAction = null;
            action();
        }

        /// <summary>
        /// 是否有待执行的动作
        /// </summary>
        public bool HasPending => mPendingAction is not null;

        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;
            Cancel();
        }
    }
}
#endif
