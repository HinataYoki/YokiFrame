using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// 抽象场景加载池基类
    /// </summary>
    public abstract class AbstractSceneResLoaderPool : ISceneResLoaderPool
    {
        private readonly Stack<ISceneResLoader> mPool = new(4);

        public ISceneResLoader Allocate() => mPool.Count > 0 ? mPool.Pop() : CreateLoader();
        public void Recycle(ISceneResLoader loader) => mPool.Push(loader);

        protected abstract ISceneResLoader CreateLoader();
    }

    /// <summary>
    /// 默认场景加载池（基于 Unity SceneManager）
    /// </summary>
    public class DefaultSceneResLoaderPool : AbstractSceneResLoaderPool
    {
        protected override ISceneResLoader CreateLoader() => new DefaultSceneResLoader(this);
    }

    /// <summary>
    /// 默认场景加载器（基于 Unity SceneManager）
    /// </summary>
    public class DefaultSceneResLoader : ISceneResLoader
    {
        private readonly ISceneResLoaderPool mPool;
        private AsyncOperation mAsyncOp;
        private Action<Scene> mOnComplete;
        private Action<float> mOnProgress;
        private Action mOnSuspended;
        private bool mIsSuspended;
        private bool mSuspendedNotified;
        private Scene mLoadedScene;
        private static SceneResCoroutineRunner sCoroutineRunner;

        public bool IsSuspended => mIsSuspended;
        public float Progress => mAsyncOp?.progress ?? 0f;

        public DefaultSceneResLoader(ISceneResLoaderPool pool) => mPool = pool;

        public void LoadAsync(string scenePath, bool isAdditive, bool suspendLoad,
            Action<Scene> onComplete, Action<float> onProgress = null,
            Action onSuspended = null)
        {
            mOnComplete = onComplete;
            mOnProgress = onProgress;
            mOnSuspended = onSuspended;
            mIsSuspended = false;
            mSuspendedNotified = false;

            var loadMode = isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            mAsyncOp = SceneManager.LoadSceneAsync(scenePath, loadMode);

            if (mAsyncOp == default)
            {
                KitLogger.Error($"[ResKit] 场景加载失败: {scenePath}");
                onComplete?.Invoke(default);
                return;
            }

            if (suspendLoad)
            {
                mAsyncOp.allowSceneActivation = false;
                mIsSuspended = true;
            }

            mAsyncOp.completed += OnLoadCompleted;

            if (mOnProgress != null || suspendLoad)
            {
                EnsureCoroutineRunner();
                sCoroutineRunner.StartCoroutine(TrackProgress());
            }
        }

        public void UnloadAsync(Scene scene, Action onComplete)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                onComplete?.Invoke();
                return;
            }

            var asyncOp = SceneManager.UnloadSceneAsync(scene);
            if (asyncOp == default)
            {
                onComplete?.Invoke();
                return;
            }

            asyncOp.completed += _ => onComplete?.Invoke();
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
            mOnComplete = null;
            mOnProgress = null;
            mOnSuspended = null;
            mIsSuspended = false;
            mSuspendedNotified = false;
            mLoadedScene = default;
            mPool?.Recycle(this);
        }

        private static void EnsureCoroutineRunner()
        {
            if (sCoroutineRunner != default) return;

            var go = new GameObject("[ResKit_SceneCoroutineRunner]");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            sCoroutineRunner = go.AddComponent<SceneResCoroutineRunner>();
        }

        private IEnumerator TrackProgress()
        {
            while (mAsyncOp != null && !mAsyncOp.isDone)
            {
                mOnProgress?.Invoke(mAsyncOp.progress);

                // 到达挂起阈值时，通知一次「已就绪」，随后等待 ResumeLoad
                if (mIsSuspended && !mSuspendedNotified && mAsyncOp.progress >= 0.9f)
                {
                    mSuspendedNotified = true;
                    mOnSuspended?.Invoke();
                }

                yield return null;
            }
            mOnProgress?.Invoke(1f);
        }

        private void OnLoadCompleted(AsyncOperation op)
        {
            mLoadedScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            mOnComplete?.Invoke(mLoadedScene);
        }
    }

    /// <summary>
    /// 场景加载协程运行器
    /// </summary>
    internal class SceneResCoroutineRunner : MonoBehaviour { }
}
