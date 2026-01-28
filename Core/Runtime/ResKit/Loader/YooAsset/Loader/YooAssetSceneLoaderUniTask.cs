#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset UniTask 场景加载池
    /// </summary>
    public class YooAssetSceneLoaderUniTaskPool : AbstractSceneResLoaderPool
    {
        private readonly ResourcePackage mPackage;
        public YooAssetSceneLoaderUniTaskPool(ResourcePackage package) => mPackage = package;
        protected override ISceneResLoader CreateLoader() => new YooAssetSceneLoaderUniTask(this, mPackage);
    }

    /// <summary>
    /// YooAsset UniTask 场景加载器
    /// </summary>
    public class YooAssetSceneLoaderUniTask : ISceneResLoaderUniTask
    {
        private readonly ISceneResLoaderPool mPool;
        private readonly ResourcePackage mPackage;
        private YooAsset.SceneHandle mHandle;
        private bool mIsSuspended;
        private string mScenePath;
        private bool mIsAdditive;

        public bool IsSuspended => mIsSuspended;
        public float Progress => mHandle?.Progress ?? 0f;

        public YooAssetSceneLoaderUniTask(ISceneResLoaderPool pool, ResourcePackage package)
        {
            mPool = pool;
            mPackage = package;
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
            mHandle = mPackage.LoadSceneAsync(scenePath, loadMode, LocalPhysicsMode.None, suspendLoad);

            if (mHandle == null)
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
            if (mHandle != null && mHandle.SceneObject.IsValid())
            {
                var unloadOp = mHandle.UnloadAsync();
                while (!unloadOp.IsDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            else if (scene.IsValid() && scene.isLoaded)
            {
                var asyncOp = SceneManager.UnloadSceneAsync(scene);
                if (asyncOp != null) await asyncOp.ToUniTask(cancellationToken: cancellationToken);
            }
        }

        public void SuspendLoad() { if (mHandle != null && !mIsSuspended) mIsSuspended = true; }
        public void ResumeLoad() { if (mHandle != null && mIsSuspended) { mHandle.ActivateScene(); mIsSuspended = false; } }

        public void UnloadAndRecycle()
        {
            SceneLoadTracker.OnUnload(this);
            mHandle = null; mIsSuspended = false;
            mScenePath = null; mIsAdditive = false;
            mPool?.Recycle(this);
        }
    }
}
#endif
