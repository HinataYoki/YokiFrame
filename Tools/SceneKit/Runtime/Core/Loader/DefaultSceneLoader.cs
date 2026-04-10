using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>
    /// 默认场景加载器，使用 Unity 原生 SceneManager
    /// </summary>
    public class DefaultSceneLoader : ISceneLoader
    {
        private readonly ISceneLoaderPool mPool;
        private AsyncOperation mAsyncOp;
        private Action<Scene> mOnComplete;
        private Action<float> mOnProgress;
        private float mSuspendAtProgress;
        private bool mIsSuspended;
        private Scene mLoadedScene;
        private MonoBehaviour mCoroutineRunner;

        /// <summary>
        /// 当前是否处于暂停状态
        /// </summary>
        public bool IsSuspended => mIsSuspended;

        /// <summary>
        /// 当前加载进度（0-1）
        /// </summary>
        public float Progress => mAsyncOp?.progress ?? 0f;

        public DefaultSceneLoader(ISceneLoaderPool pool)
        {
            mPool = pool;
        }

        /// <summary>
        /// 异步加载场景（通过场景名）
        /// </summary>
        public void LoadAsync(string sceneName, SceneLoadMode mode,
            Action<Scene> onComplete,
            Action<float> onProgress = null,
            float suspendAtProgress = 1f)
        {
            mOnComplete = onComplete;
            mOnProgress = onProgress;
            mSuspendAtProgress = Mathf.Clamp01(suspendAtProgress);
            mIsSuspended = false;

            var loadMode = mode == SceneLoadMode.Single 
                ? LoadSceneMode.Single 
                : LoadSceneMode.Additive;

            mAsyncOp = SceneManager.LoadSceneAsync(sceneName, loadMode);
            if (mAsyncOp == null)
            {
                KitLogger.Error($"[SceneKit] 场景加载失败: {sceneName}");
                onComplete?.Invoke(default);
                return;
            }

            // 如果需要暂停，先禁止自动激活
            if (mSuspendAtProgress < 1f)
            {
                mAsyncOp.allowSceneActivation = false;
            }

            StartProgressTracking(sceneName);
        }

        /// <summary>
        /// 异步加载场景（通过 BuildIndex）
        /// </summary>
        public void LoadAsync(int buildIndex, SceneLoadMode mode,
            Action<Scene> onComplete,
            Action<float> onProgress = null,
            float suspendAtProgress = 1f)
        {
            mOnComplete = onComplete;
            mOnProgress = onProgress;
            mSuspendAtProgress = Mathf.Clamp01(suspendAtProgress);
            mIsSuspended = false;

            var loadMode = mode == SceneLoadMode.Single 
                ? LoadSceneMode.Single 
                : LoadSceneMode.Additive;

            mAsyncOp = SceneManager.LoadSceneAsync(buildIndex, loadMode);
            if (mAsyncOp == null)
            {
                KitLogger.Error($"[SceneKit] 场景加载失败: BuildIndex={buildIndex}");
                onComplete?.Invoke(default);
                return;
            }

            // 如果需要暂停，先禁止自动激活
            if (mSuspendAtProgress < 1f)
            {
                mAsyncOp.allowSceneActivation = false;
            }

            StartProgressTracking(buildIndex.ToString());
        }

        /// <summary>
        /// 异步卸载场景
        /// </summary>
        public void UnloadAsync(Scene scene, Action onComplete)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                KitLogger.Warning($"[SceneKit] 尝试卸载无效或未加载的场景");
                onComplete?.Invoke();
                return;
            }

            var asyncOp = SceneManager.UnloadSceneAsync(scene);
            if (asyncOp == null)
            {
                KitLogger.Error($"[SceneKit] 场景卸载失败: {scene.name}");
                onComplete?.Invoke();
                return;
            }

            asyncOp.completed += _ => onComplete?.Invoke();
        }

        /// <summary>
        /// 暂停加载
        /// </summary>
        public void SuspendLoad()
        {
            if (mAsyncOp != null && !mIsSuspended)
            {
                mAsyncOp.allowSceneActivation = false;
                mIsSuspended = true;
            }
        }

        /// <summary>
        /// 恢复加载
        /// </summary>
        public void ResumeLoad()
        {
            if (mAsyncOp != null && mIsSuspended)
            {
                mAsyncOp.allowSceneActivation = true;
                mIsSuspended = false;
            }
        }

        /// <summary>
        /// 回收加载器到对象池
        /// </summary>
        public void Recycle()
        {
            mAsyncOp = null;
            mOnComplete = null;
            mOnProgress = null;
            mSuspendAtProgress = 1f;
            mIsSuspended = false;
            mLoadedScene = default;
            mPool?.Recycle(this);
        }

        /// <summary>
        /// 启动进度追踪
        /// </summary>
        private void StartProgressTracking(string sceneIdentifier)
        {
            // 使用 completed 回调处理加载完成
            mAsyncOp.completed += OnLoadCompleted;

            // 如果需要进度回调或暂停功能，启动协程追踪
            if (mOnProgress != null || mSuspendAtProgress < 1f)
            {
                EnsureCoroutineRunner();
                mCoroutineRunner?.StartCoroutine(TrackProgress());
            }
        }

        /// <summary>
        /// 确保有协程运行器
        /// </summary>
        private void EnsureCoroutineRunner()
        {
            if (mCoroutineRunner != null) return;

            var go = new GameObject("[SceneKit_CoroutineRunner]");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            mCoroutineRunner = go.AddComponent<SceneKitCoroutineRunner>();
        }

        /// <summary>
        /// 追踪加载进度的协程
        /// </summary>
        private IEnumerator TrackProgress()
        {
            while (mAsyncOp != null && !mAsyncOp.isDone)
            {
                float progress = mAsyncOp.progress;
                
                // 报告进度
                mOnProgress?.Invoke(progress);

                // 检查是否需要暂停（Unity 的 progress 在 allowSceneActivation=false 时最大为 0.9）
                if (!mIsSuspended && mSuspendAtProgress < 1f && progress >= mSuspendAtProgress * 0.9f)
                {
                    mIsSuspended = true;
                    mOnProgress?.Invoke(mSuspendAtProgress);
                }

                yield return null;
            }

            // 最终进度
            mOnProgress?.Invoke(1f);
        }

        /// <summary>
        /// 加载完成回调
        /// </summary>
        private void OnLoadCompleted(AsyncOperation op)
        {
            // 获取最后加载的场景
            mLoadedScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            mOnComplete?.Invoke(mLoadedScene);
        }
    }

    /// <summary>
    /// 协程运行器组件
    /// </summary>
    internal class SceneKitCoroutineRunner : MonoBehaviour { }
}
