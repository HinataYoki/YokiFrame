#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 场景加载池
    /// </summary>
    public class YooAssetSceneLoaderPool : AbstractSceneResLoaderPool
    {
        protected readonly ResourcePackage mPackage;

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
        private YooAsset.SceneHandle mHandle;
        private Action<Scene> mOnComplete;
        private Action<float> mOnProgress;
        private bool mIsSuspended;
        private string mScenePath;
        private bool mIsAdditive;
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
            mScenePath = scenePath;
            mIsAdditive = isAdditive;

            var loadMode = isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            mHandle = mPackage.LoadSceneAsync(scenePath, loadMode, LocalPhysicsMode.None, suspendLoad);

            if (mHandle == null)
            {
                KitLogger.Error($"[ResKit] YooAsset 场景加载失败: {scenePath}");
                onComplete?.Invoke(default);
                return;
            }

            if (suspendLoad) mIsSuspended = true;
            mHandle.Completed += OnLoadCompleted;

            if (mOnProgress != null)
            {
                EnsureCoroutineRunner();
                sCoroutineRunner.StartCoroutine(TrackProgress());
            }
        }

        public void UnloadAsync(Scene scene, Action onComplete)
        {
            if (mHandle != default && mHandle.SceneObject.IsValid())
            {
                var unloadOp = mHandle.UnloadAsync();
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

        public void SuspendLoad() { if (mHandle != default && !mIsSuspended) mIsSuspended = true; }
        public void ResumeLoad() { if (mHandle != default && mIsSuspended) { mHandle.ActivateScene(); mIsSuspended = false; } }

        public void UnloadAndRecycle()
        {
            SceneLoadTracker.OnUnload(this);
            mHandle = null; mOnComplete = null; mOnProgress = null; mIsSuspended = false;
            mScenePath = null; mIsAdditive = false;
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
            while (mHandle != default && !mHandle.IsDone) { mOnProgress?.Invoke(mHandle.Progress); yield return null; }
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
