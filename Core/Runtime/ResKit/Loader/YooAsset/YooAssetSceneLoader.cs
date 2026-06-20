#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 场景加载器。
    /// 版本无关 — V2/V3 差异通过 IYooAssetSceneProvider 隔离。
    /// 本文件零内部 #if。
    /// </summary>
    public sealed class YooAssetSceneLoader : ISceneResLoader
    {
        private readonly ISceneResLoaderPool mPool;
        private readonly IYooAssetSceneProvider mProvider;
        private YooAsset.SceneHandle mHandle;
        private Action<Scene> mOnComplete;
        private Action<float> mOnProgress;
        private Action mOnSuspended;
        private bool mIsSuspended;
        private bool mSuspendedNotified;
        private string mScenePath;
        private bool mIsAdditive;
        private static SceneResCoroutineRunner sCoroutineRunner;

        public bool IsSuspended => mIsSuspended;
        public float Progress => mHandle?.Progress ?? 0f;

        internal YooAssetSceneLoader(ISceneResLoaderPool pool, IYooAssetSceneProvider provider)
        {
            mPool = pool;
            mProvider = provider;
        }

        public void LoadAsync(string scenePath, bool isAdditive, bool suspendLoad,
            Action<Scene> onComplete, Action<float> onProgress = null,
            Action onSuspended = null)
        {
            mOnComplete = onComplete;
            mOnProgress = onProgress;
            mOnSuspended = onSuspended;
            mIsSuspended = false;
            mSuspendedNotified = false;
            mScenePath = scenePath;
            mIsAdditive = isAdditive;

            var loadMode = isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            mHandle = mProvider.LoadSceneAsync(scenePath, loadMode, !suspendLoad);

            if (mHandle == default)
            {
                KitLogger.Error($"[ResKit] YooAsset 场景加载失败: {scenePath}");
                onComplete?.Invoke(default);
                return;
            }

            if (suspendLoad) mIsSuspended = true;
            mHandle.Completed += OnLoadCompleted;

            // 需要进度回调，或处于挂起态（需追踪到达阈值以触发 onSuspended）时启动协程
            if (mOnProgress != null || suspendLoad)
            {
                EnsureCoroutineRunner();
                sCoroutineRunner.StartCoroutine(TrackProgress());
            }
        }

        public void UnloadAsync(Scene scene, Action onComplete)
        {
            if (mHandle != default && mHandle.SceneObject.IsValid())
            {
                var unloadOp = mProvider.UnloadSceneAsync(mHandle);
                unloadOp.Completed += _ => onComplete?.Invoke();
            }
            else if (scene.IsValid() && scene.isLoaded)
            {
                var asyncOp = SceneManager.UnloadSceneAsync(scene);
                if (asyncOp != default) { asyncOp.completed += _ => onComplete?.Invoke(); return; }
                onComplete?.Invoke();
            }
            else onComplete?.Invoke();
        }

        public void SuspendLoad()
        {
            if (mHandle != default && !mIsSuspended) mIsSuspended = true;
        }

        public void ResumeLoad()
        {
            if (mHandle != default && mIsSuspended)
            {
                // 解除挂起，让加载操作跑完（而非 ActivateScene，后者要求场景已 loaded）
                // 经 provider 隔离 V2（UnSuspend）/ V3（AllowSceneActivation）的 API 差异
                mProvider.ResumeSuspendedScene(mHandle);
                mIsSuspended = false;
            }
        }

        public void UnloadAndRecycle()
        {
            SceneLoadTracker.OnUnload(this);
            mHandle = default;
            mOnComplete = null;
            mOnProgress = null;
            mOnSuspended = null;
            mIsSuspended = false;
            mSuspendedNotified = false;
            mScenePath = null;
            mIsAdditive = false;
            mProvider.ReleaseHandle();
            mPool?.Recycle(this);
        }

        private static void EnsureCoroutineRunner()
        {
            if (sCoroutineRunner != default) return;
            var go = new GameObject("[ResKit_YooAssetSceneCoroutineRunner]");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            sCoroutineRunner = go.AddComponent<SceneResCoroutineRunner>();
        }

        private IEnumerator TrackProgress()
        {
            while (mHandle != default && !mHandle.IsDone)
            {
                mOnProgress?.Invoke(mHandle.Progress);

                // 到达挂起阈值时，通知一次「已就绪」，随后等待 ResumeLoad
                if (mIsSuspended && !mSuspendedNotified && mHandle.Progress >= 0.9f)
                {
                    mSuspendedNotified = true;
                    mOnSuspended?.Invoke();
                }

                yield return null;
            }
            mOnProgress?.Invoke(1f);
        }

        private void OnLoadCompleted(YooAsset.SceneHandle handle)
        {
            var scene = handle.SceneObject;
            SceneLoadTracker.OnLoad(this, mScenePath, scene, mIsAdditive);
            mOnComplete?.Invoke(scene);
        }
    }
}
#endif
