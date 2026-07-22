#if UNITY_2022_3_OR_NEWER
using System;

namespace YokiFrame.Unity
{
    /// <summary>封装 Unity AsyncOperation，并在 YokiFrame 帧循环中报告进度阈值。</summary>
    internal sealed class UnitySceneLoadOperation : IResSceneLoadOperation, IYokiFrameUpdateListener
    {
        private readonly UnityEngine.AsyncOperation mOperation;
        private readonly float mSuspendAtProgress;
        private readonly Action<float> mOnProgress;
        private readonly Action mOnSuspended;
        private bool mSuspendedNotified;
        private bool mRecycled;

        /// <summary>创建 Unity 场景加载操作。</summary>
        public UnitySceneLoadOperation(
            UnityEngine.AsyncOperation operation,
            float suspendAtProgress,
            Action<float> onProgress,
            Action onSuspended)
        {
            mOperation = operation;
            mSuspendAtProgress = suspendAtProgress < 0f ? 0f : suspendAtProgress > 1f ? 1f : suspendAtProgress;
            mOnProgress = onProgress;
            mOnSuspended = onSuspended;
            if (mOperation != null && mSuspendAtProgress < 1f)
            {
                mOperation.allowSceneActivation = false;
                YokiFrameUpdateDispatcher.Register(this);
            }
        }

        /// <inheritdoc />
        public bool IsSuspended => mOperation != null && !mOperation.allowSceneActivation;

        /// <inheritdoc />
        public float Progress => mOperation == null ? 0f : mOperation.progress;

        /// <summary>发送首次进度，保证 SceneKit 在启动时获得 0 到 1 的起点。</summary>
        public void ReportInitialProgress()
        {
            mOnProgress?.Invoke(Progress);
        }

        /// <summary>标记 Unity 完成回调已经触发。</summary>
        public void MarkCompleted()
        {
            YokiFrameUpdateDispatcher.Unregister(this);
        }

        /// <inheritdoc />
        public void SuspendLoad()
        {
            if (mOperation != null)
            {
                mOperation.allowSceneActivation = false;
            }
        }

        /// <inheritdoc />
        public void ResumeLoad()
        {
            if (mOperation != null)
            {
                mOperation.allowSceneActivation = true;
            }
        }

        /// <inheritdoc />
        public void Recycle()
        {
            if (mRecycled)
            {
                return;
            }

            mRecycled = true;
            YokiFrameUpdateDispatcher.Unregister(this);
        }

        /// <inheritdoc />
        public void OnFrameUpdate(float scaledDeltaTime, float unscaledDeltaTime)
        {
            if (mRecycled || mOperation == null)
            {
                return;
            }

            mOnProgress?.Invoke(Progress);
            if (!mSuspendedNotified && !mOperation.allowSceneActivation && Progress >= mSuspendAtProgress)
            {
                mSuspendedNotified = true;
                mOnSuspended?.Invoke();
            }
        }

        /// <inheritdoc />
        public void OnHostReset()
        {
            Recycle();
        }
    }
}
#endif
