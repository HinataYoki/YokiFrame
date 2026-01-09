#if YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// 默认 UniTask 场景加载池
    /// </summary>
    public class DefaultSceneResLoaderUniTaskPool : AbstractSceneResLoaderPool
    {
        protected override ISceneResLoader CreateLoader() => new DefaultSceneResLoaderUniTask(this);
    }

    /// <summary>
    /// 默认 UniTask 场景加载器
    /// </summary>
    public class DefaultSceneResLoaderUniTask : ISceneResLoaderUniTask
    {
        private readonly ISceneResLoaderPool mPool;
        private AsyncOperation mAsyncOp;
        private bool mIsSuspended;
        private Scene mLoadedScene;

        public bool IsSuspended => mIsSuspended;
        public float Progress => mAsyncOp?.progress ?? 0f;

        public DefaultSceneResLoaderUniTask(ISceneResLoaderPool pool) => mPool = pool;

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
            var loadMode = isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            mAsyncOp = SceneManager.LoadSceneAsync(scenePath, loadMode);

            if (mAsyncOp == null)
            {
                KitLogger.Error($"[ResKit] 场景加载失败: {scenePath}");
                return default;
            }

            if (suspendLoad)
            {
                mAsyncOp.allowSceneActivation = false;
                mIsSuspended = true;
            }

            // 使用 UniTask 等待加载完成
            while (!mAsyncOp.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(mAsyncOp.progress);

                // 如果暂停了，等待恢复
                if (mIsSuspended && mAsyncOp.progress >= 0.9f)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    continue;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            progress?.Report(1f);
            mLoadedScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            return mLoadedScene;
        }

        public void UnloadAsync(Scene scene, Action onComplete)
        {
            UnloadUniTaskAsync(scene).ContinueWith(() => onComplete?.Invoke()).Forget();
        }

        public async UniTask UnloadUniTaskAsync(Scene scene, CancellationToken cancellationToken = default)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            var asyncOp = SceneManager.UnloadSceneAsync(scene);
            if (asyncOp == null) return;

            await asyncOp.ToUniTask(cancellationToken: cancellationToken);
        }

        public void SuspendLoad()
        {
            if (mAsyncOp != null && !mIsSuspended)
            {
                mAsyncOp.allowSceneActivation = false;
                mIsSuspended = true;
            }
        }

        public void ResumeLoad()
        {
            if (mAsyncOp != null && mIsSuspended)
            {
                mAsyncOp.allowSceneActivation = true;
                mIsSuspended = false;
            }
        }

        public void UnloadAndRecycle()
        {
            mAsyncOp = null;
            mIsSuspended = false;
            mLoadedScene = default;
            mPool?.Recycle(this);
        }
    }
}
#endif
