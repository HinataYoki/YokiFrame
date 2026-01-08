#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 场景加载池
    /// </summary>
    public class YooAssetSceneLoaderPool : AbstractSceneResLoaderPool
    {
        private readonly ResourcePackage mPackage;

        public YooAssetSceneLoaderPool(ResourcePackage package) => mPackage = package;

        protected override ISceneResLoader CreateLoader() => new YooAssetSceneLoader(this, mPackage);
    }

    /// <summary>
    /// YooAsset 场景加载器
    /// </summary>
    public class YooAssetSceneLoader : ISceneResLoader
    {
        private readonly ISceneResLoaderPool mPool;
        private readonly ResourcePackage mPackage;
        private SceneHandle mHandle;
        private Action<Scene> mOnComplete;
        private Action<float> mOnProgress;
        private bool mIsSuspended;
        private static SceneResCoroutineRunner sCoroutineRunner;

        public bool IsSuspended => mIsSuspended;
        public float Progress => mHandle?.Progress ?? 0f;

        public YooAssetSceneLoader(ISceneResLoaderPool pool, ResourcePackage package)
        {
            mPool = pool;
            mPackage = package;
        }

        public void LoadAsync(string scenePath, bool isAdditive, bool suspendLoad,
            Action<Scene> onComplete, Action<float> onProgress = null)
        {
            mOnComplete = onComplete;
            mOnProgress = onProgress;
            mIsSuspended = false;

            var loadMode = isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            mHandle = mPackage.LoadSceneAsync(scenePath, loadMode, suspendLoad);

            if (mHandle == null)
            {
                KitLogger.Error($"[ResKit] YooAsset 场景加载失败: {scenePath}");
                onComplete?.Invoke(default);
                return;
            }

            if (suspendLoad)
            {
                mIsSuspended = true;
            }

            mHandle.Completed += OnLoadCompleted;

            if (mOnProgress != null)
            {
                EnsureCoroutineRunner();
                sCoroutineRunner.StartCoroutine(TrackProgress());
            }
        }

        public void UnloadAsync(Scene scene, Action onComplete)
        {
            if (mHandle != null && mHandle.SceneObject.IsValid())
            {
                var unloadOp = mHandle.UnloadAsync();
                unloadOp.Completed += _ => onComplete?.Invoke();
            }
            else
            {
                // 回退到 Unity 原生卸载
                if (scene.IsValid() && scene.isLoaded)
                {
                    var asyncOp = SceneManager.UnloadSceneAsync(scene);
                    if (asyncOp != null)
                    {
                        asyncOp.completed += _ => onComplete?.Invoke();
                        return;
                    }
                }
                onComplete?.Invoke();
            }
        }

        public void SuspendLoad()
        {
            if (mHandle != null && !mIsSuspended)
            {
                // YooAsset 的 SceneHandle 不支持运行时暂停，只能在创建时指定
                mIsSuspended = true;
            }
        }

        public void ResumeLoad()
        {
            if (mHandle != null && mIsSuspended)
            {
                mHandle.ActivateScene();
                mIsSuspended = false;
            }
        }

        public void UnloadAndRecycle()
        {
            mHandle = null;
            mOnComplete = null;
            mOnProgress = null;
            mIsSuspended = false;
            mPool?.Recycle(this);
        }

        private static void EnsureCoroutineRunner()
        {
            if (sCoroutineRunner != null) return;

            var go = new GameObject("[ResKit_YooAssetSceneCoroutineRunner]");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            sCoroutineRunner = go.AddComponent<SceneResCoroutineRunner>();
        }

        private IEnumerator TrackProgress()
        {
            while (mHandle != null && !mHandle.IsDone)
            {
                mOnProgress?.Invoke(mHandle.Progress);
                yield return null;
            }
            mOnProgress?.Invoke(1f);
        }

        private void OnLoadCompleted(SceneHandle handle)
        {
            mOnComplete?.Invoke(handle.SceneObject);
        }
    }

#if YOKIFRAME_UNITASK_SUPPORT
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
        private SceneHandle mHandle;
        private bool mIsSuspended;

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
            var loadMode = isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            mHandle = mPackage.LoadSceneAsync(scenePath, loadMode, suspendLoad);

            if (mHandle == null)
            {
                KitLogger.Error($"[ResKit] YooAsset 场景加载失败: {scenePath}");
                return default;
            }

            if (suspendLoad)
            {
                mIsSuspended = true;
            }

            // 使用 UniTask 等待加载完成
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
            return mHandle.SceneObject;
        }

        public void UnloadAsync(Scene scene, Action onComplete)
        {
            UnloadUniTaskAsync(scene).ContinueWith(() => onComplete?.Invoke()).Forget();
        }

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
                if (asyncOp != null)
                {
                    await asyncOp.ToUniTask(cancellationToken: cancellationToken);
                }
            }
        }

        public void SuspendLoad()
        {
            if (mHandle != null && !mIsSuspended)
            {
                mIsSuspended = true;
            }
        }

        public void ResumeLoad()
        {
            if (mHandle != null && mIsSuspended)
            {
                mHandle.ActivateScene();
                mIsSuspended = false;
            }
        }

        public void UnloadAndRecycle()
        {
            mHandle = null;
            mIsSuspended = false;
            mPool?.Recycle(this);
        }
    }
#endif
}
#endif
