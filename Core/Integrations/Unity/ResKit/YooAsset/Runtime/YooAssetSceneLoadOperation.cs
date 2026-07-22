#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3

using System;
using YooSceneHandle = YooAsset.SceneHandle;

namespace YokiFrame.Unity
{
    /// <summary>封装 YooAsset 场景句柄，并通过统一帧循环报告进度和挂起阈值。</summary>
    internal sealed class YooAssetSceneLoadOperation : IResSceneLoadOperation, IYokiFrameUpdateListener
    {
        private readonly YooSceneHandle mHandle;
        private readonly float mSuspendAtProgress;
        private readonly Action<float> mOnProgress;
        private readonly Action mOnSuspended;
        private bool mSceneActivationAllowed;
        private bool mSuspensionReached;
        private bool mRecycled;

        /// <summary>创建 YooAsset 场景加载操作。</summary>
        internal YooAssetSceneLoadOperation(
            YooSceneHandle handle,
            float suspendAtProgress,
            Action<float> onProgress,
            Action onSuspended)
        {
            mHandle = handle;
            mSuspendAtProgress = suspendAtProgress < 0f ? 0f : suspendAtProgress > 1f ? 1f : suspendAtProgress;
            mOnProgress = onProgress;
            mOnSuspended = onSuspended;
            mSceneActivationAllowed = mSuspendAtProgress >= 1f;
            if (mHandle != null && mSuspendAtProgress < 1f)
            {
                YokiFrameUpdateDispatcher.Register(this);
            }
        }

        /// <inheritdoc />
        public float Progress => mHandle == null ? 0f : mHandle.Progress;

        /// <inheritdoc />
        public bool IsSuspended => mHandle != null
                                  && !mHandle.IsDone
                                  && mSuspensionReached
                                  && !mSceneActivationAllowed;

        /// <summary>发送场景请求初始进度。</summary>
        internal void ReportInitialProgress()
        {
            mOnProgress?.Invoke(Progress);
        }

        /// <summary>在 YooAsset 完成回调后停止帧监听。</summary>
        internal void MarkCompleted()
        {
            mSceneActivationAllowed = true;
            YokiFrameUpdateDispatcher.Unregister(this);
        }

        /// <inheritdoc />
        public void SuspendLoad()
        {
            // YooAsset 只能在创建 SceneHandle 时决定是否暂停激活，运行中不能重新挂起。
        }

        /// <inheritdoc />
        public void ResumeLoad()
        {
            if (mHandle == null || mHandle.IsDone || mSceneActivationAllowed)
            {
                return;
            }

#if YOKIFRAME_YOOASSET_3
            bool activationAllowed = mHandle.AllowSceneActivation();
#else
            bool activationAllowed = mHandle.UnSuspend();
#endif
            if (activationAllowed)
            {
                mSceneActivationAllowed = true;
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
            if (mRecycled || mHandle == null)
            {
                return;
            }

            mOnProgress?.Invoke(Progress);
            if (!mSuspensionReached
                && !mSceneActivationAllowed
                && !mHandle.IsDone
                && Progress >= mSuspendAtProgress)
            {
                mSuspensionReached = true;
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
