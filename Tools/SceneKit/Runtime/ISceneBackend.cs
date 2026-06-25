using System;

namespace YokiFrame
{
    /// <summary>
    /// 定义引擎侧场景加载、卸载和激活能力。
    /// </summary>
    public interface ISceneBackend
    {
        /// <summary>
        /// 获取后端名称。
        /// </summary>
        string BackendName { get; }

        /// <summary>
        /// 获取当前激活场景句柄。
        /// </summary>
        SceneHandle ActiveScene { get; }

        /// <summary>
        /// 异步加载场景。
        /// </summary>
        /// <param name="request">加载请求。</param>
        /// <param name="onComplete">完成回调。</param>
        /// <param name="onProgress">进度回调。</param>
        /// <param name="onSuspended">挂起回调。</param>
        /// <returns>加载操作。</returns>
        ISceneLoadOperation LoadSceneAsync(
            SceneLoadRequest request,
            Action<SceneLoadResult> onComplete,
            Action<float> onProgress,
            Action onSuspended);

        /// <summary>
        /// 异步卸载场景。
        /// </summary>
        /// <param name="scene">场景句柄。</param>
        /// <param name="onComplete">完成回调。</param>
        void UnloadSceneAsync(SceneHandle scene, Action onComplete);

        /// <summary>
        /// 设置当前激活场景。
        /// </summary>
        /// <param name="scene">场景句柄。</param>
        void SetActiveScene(SceneHandle scene);

        /// <summary>
        /// 获取当前激活场景。
        /// </summary>
        /// <returns>当前激活场景句柄。</returns>
        SceneHandle GetActiveScene();

        /// <summary>
        /// 卸载未使用资源。
        /// </summary>
        /// <param name="onComplete">完成回调。</param>
        void UnloadUnusedAssets(Action onComplete);
    }
}
