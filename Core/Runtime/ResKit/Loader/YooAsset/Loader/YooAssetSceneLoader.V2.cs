#if YOKIFRAME_YOOASSET_SUPPORT && !YOOASSET_3_0_OR_NEWER
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 2.x 场景加载池
    /// </summary>
    public class YooAssetSceneLoaderPool : AbstractSceneResLoaderPool
    {
        public YooAssetSceneLoaderPool() { }

        protected override ISceneResLoader CreateLoader() => new YooAssetSceneLoader(this);
    }

    /// <summary>
    /// YooAsset 2.x 场景加载器
    /// 2.x LoadSceneAsync(path, loadMode, activateOnLoad) — 无 LocalPhysicsMode / suspendLoad 参数
    /// </summary>
    public class YooAssetSceneLoader : ISceneResLoader
    {
        private readonly ISceneResLoaderPool mPool;
        private YooAsset.SceneHandle mHandle;
        private Action<Scene> mOnComplete;
        private Action<float> mOnProgress;
        private bool mIsSuspended;
        private string mScenePath;
        private bool mIsAdditive;
        private static SceneResCoroutineRunner sCoroutineRunner;

        public bool IsSuspended => mIsSuspended;
        public float Progress => mHandle?.Progress ?? 0f;

        public YooAssetSceneLoader(ISceneResLoaderPool pool)
        {
            mPool = pool;
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
            // 2.3.x: LoadSceneAsync(path, loadMode, physicsMode, allowSceneActivation)
            // suspendLoad 时 allowSceneActivation=false，加载完成后需手动 ActivateScene()
            mHandle = YooAssets.LoadSceneAsync(scenePath, loadMode, LocalPhysicsMode.None, !suspendLoad);

            if (mHandle == default)
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

        public void SuspendLoad()
        {
            if (mHandle != default && !mIsSuspended) mIsSuspended = true;
        }

        public void ResumeLoad()
        {
            if (mHandle != default && mIsSuspended)
            {
                // 2.x: 调用 ActivateScene() 激活已加载的场景
                mHandle.ActivateScene();
                mIsSuspended = false;
            }
        }

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
