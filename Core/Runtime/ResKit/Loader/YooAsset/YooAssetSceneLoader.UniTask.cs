#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 场景加载器 UniTask 扩展。
    /// 继承版本无关的 YooAssetSceneLoader，仅追加 UniTask 异步方法。
    /// 使用 UniTask 轮询替代 Coroutine 进度追踪。
    /// </summary>
    public sealed class YooAssetSceneLoaderUniTask : ISceneResLoaderUniTask
    {
        private readonly ISceneResLoaderPool mPool;
        private readonly IYooAssetSceneProvider mProvider;
        private YooAsset.SceneHandle mHandle;
        private bool mIsSuspended;
        private string mScenePath;
        private bool mIsAdditive;

        public bool IsSuspended => mIsSuspended;
        public float Progress => mHandle?.Progress ?? 0f;

        internal YooAssetSceneLoaderUniTask(ISceneResLoaderPool pool, IYooAssetSceneProvider provider)
        {
            mPool = pool;
            mProvider = provider;
        }

        public void LoadAsync(string scenePath, bool isAdditive, bool suspendLoad,
            Action<Scene> onComplete, Action<float> onProgress = null)
        {
            LoadUniTaskAsync(scenePath, isAdditive, suspendLoad,
                onProgress != null ? new Progress<float>(onProgress) : null)
                .ContinueWith(scene => onComplete?.Invoke(scene)).Forget();
        }

        public async UniTask<Scene> LoadUniTaskAsync(string scenePath, bool isAdditive, bool suspendLoad,
            IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {
            mIsSuspended = false;
            mScenePath = scenePath;
            mIsAdditive = isAdditive;

            var loadMode = isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            mHandle = mProvider.LoadSceneAsync(scenePath, loadMode, !suspendLoad);

            if (mHandle == default)
            {
                KitLogger.Error($"[ResKit] YooAsset 场景加载失败: {scenePath}");
                return default;
            }

            if (suspendLoad) mIsSuspended = true;

            while (!mHandle.IsDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(mHandle.Progress);
                if (mIsSuspended && mHandle.Progress >= 0.9f)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    continue;
                }
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            progress?.Report(1f);
            var scene = mHandle.SceneObject;
            SceneLoadTracker.OnLoad(this, mScenePath, scene, mIsAdditive);
            return scene;
        }

        public void UnloadAsync(Scene scene, Action onComplete)
            => UnloadUniTaskAsync(scene).ContinueWith(() => onComplete?.Invoke()).Forget();

        public async UniTask UnloadUniTaskAsync(Scene scene, CancellationToken cancellationToken = default)
        {
            if (mHandle != default && mHandle.SceneObject.IsValid())
            {
                var unloadOp = mProvider.UnloadSceneAsync(mHandle);
                while (!unloadOp.IsDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            else if (scene.IsValid() && scene.isLoaded)
            {
                var asyncOp = SceneManager.UnloadSceneAsync(scene);
                if (asyncOp != default) await asyncOp.ToUniTask(cancellationToken: cancellationToken);
            }
        }

        public void SuspendLoad()
        {
            if (mHandle != default && !mIsSuspended) mIsSuspended = true;
        }

        public void ResumeLoad()
        {
            if (mHandle != default && mIsSuspended)
            {
                mHandle.ActivateScene();
                mIsSuspended = false;
            }
        }

        public void UnloadAndRecycle()
        {
            SceneLoadTracker.OnUnload(this);
            mHandle = default;
            mIsSuspended = false;
            mScenePath = null;
            mIsAdditive = false;
            mProvider.ReleaseHandle();
            mPool?.Recycle(this);
        }
    }
}
#endif
