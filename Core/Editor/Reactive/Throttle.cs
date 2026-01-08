#if UNITY_EDITOR
using System;
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 节流器 - 限制高频调用，在指定时间间隔内只执行一次
    /// 适用于进度更新等高频事件
    /// </summary>
    public sealed class Throttle : IDisposable
    {
        private readonly float mInterval;
        private double mLastExecuteTime;
        private Action mPendingAction;
        private bool mIsDisposed;
        private bool mHasPending;

        /// <summary>
        /// 创建节流器
        /// </summary>
        /// <param name="intervalSeconds">最小执行间隔（秒）</param>
        public Throttle(float intervalSeconds)
        {
            mInterval = intervalSeconds;
            mLastExecuteTime = -intervalSeconds; // 确保首次立即执行
        }

        /// <summary>
        /// 执行动作（受节流限制）
        /// </summary>
        public void Execute(Action action)
        {
            if (mIsDisposed || action == null) return;

            var now = EditorApplication.timeSinceStartup;
            var elapsed = now - mLastExecuteTime;

            if (elapsed >= mInterval)
            {
                // 超过间隔，立即执行
                mLastExecuteTime = now;
                mHasPending = false;
                action();
            }
            else
            {
                // 在间隔内，缓存待执行动作
                mPendingAction = action;
                mHasPending = true;
            }
        }

        /// <summary>
        /// 强制执行待处理的动作（如果有）
        /// </summary>
        public void Flush()
        {
            if (mIsDisposed || !mHasPending) return;

            mLastExecuteTime = EditorApplication.timeSinceStartup;
            mHasPending = false;
            mPendingAction?.Invoke();
            mPendingAction = null;
        }

        /// <summary>
        /// 重置节流器状态
        /// </summary>
        public void Reset()
        {
            mLastExecuteTime = -mInterval;
            mPendingAction = null;
            mHasPending = false;
        }

        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;
            mPendingAction = null;
        }
    }
}
#endif
