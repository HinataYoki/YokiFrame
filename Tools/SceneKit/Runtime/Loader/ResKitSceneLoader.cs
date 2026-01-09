using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// 基于 ResKit 的场景加载器池
    /// 默认使用 ResKit 的场景加载池，支持 YooAsset 等扩展
    /// </summary>
    public class ResKitSceneLoaderPool : ISceneLoaderPool
    {
        private readonly Stack<ISceneLoader> mPool = new(4);

        public ISceneLoader Allocate()
        {
            if (mPool.Count > 0)
            {
                return mPool.Pop();
            }
            return new ResKitSceneLoader(this);
        }

        public void Recycle(ISceneLoader loader)
        {
            if (loader != null)
            {
                mPool.Push(loader);
            }
        }
    }

    /// <summary>
    /// 基于 ResKit 的场景加载器
    /// 委托给 ResKit 的场景加载器实现，支持 YooAsset 等扩展
    /// </summary>
    public class ResKitSceneLoader : ISceneLoader
    {
        private readonly ISceneLoaderPool mPool;
        private ISceneResLoader mResLoader;
        private Action<Scene> mOnComplete;
        private Action<float> mOnProgress;
        private float mSuspendAtProgress;

        public bool IsSuspended => mResLoader?.IsSuspended ?? false;
        public float Progress => mResLoader?.Progress ?? 0f;

        public ResKitSceneLoader(ISceneLoaderPool pool) => mPool = pool;

        public void LoadAsync(string sceneName, SceneLoadMode mode,
            Action<Scene> onComplete,
            Action<float> onProgress = null,
            float suspendAtProgress = 1f)
        {
            mOnComplete = onComplete;
            mOnProgress = onProgress;
            mSuspendAtProgress = suspendAtProgress;

            // 从 ResKit 获取场景加载器
            mResLoader = ResKit.GetSceneLoaderPool().Allocate();

            bool isAdditive = mode == SceneLoadMode.Additive;
            bool suspendLoad = suspendAtProgress < 1f;

            mResLoader.LoadAsync(sceneName, isAdditive, suspendLoad,
                OnSceneLoaded,
                OnProgressUpdate);
        }

        public void LoadAsync(int buildIndex, SceneLoadMode mode,
            Action<Scene> onComplete,
            Action<float> onProgress = null,
            float suspendAtProgress = 1f)
        {
            // 通过 BuildIndex 获取场景路径
            string scenePath = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(buildIndex);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

            LoadAsync(sceneName, mode, onComplete, onProgress, suspendAtProgress);
        }

        public void UnloadAsync(Scene scene, Action onComplete)
        {
            if (mResLoader != null)
            {
                mResLoader.UnloadAsync(scene, onComplete);
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

        public void SuspendLoad() => mResLoader?.SuspendLoad();

        public void ResumeLoad() => mResLoader?.ResumeLoad();

        public void Recycle()
        {
            mResLoader?.UnloadAndRecycle();
            mResLoader = null;
            mOnComplete = null;
            mOnProgress = null;
            mSuspendAtProgress = 1f;
            mPool?.Recycle(this);
        }

        private void OnSceneLoaded(Scene scene)
        {
            mOnComplete?.Invoke(scene);
        }

        private void OnProgressUpdate(float progress)
        {
            mOnProgress?.Invoke(progress);

            // 检查是否需要暂停
            if (mSuspendAtProgress < 1f && progress >= mSuspendAtProgress * 0.9f && !IsSuspended)
            {
                mResLoader?.SuspendLoad();
            }
        }
    }
}
