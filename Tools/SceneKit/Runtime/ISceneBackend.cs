using System;

namespace YokiFrame
{
    /// <summary>定义 SceneKit 显式覆盖后端的场景生命周期能力。</summary>
    public interface ISceneBackend
    {
        /// <summary>获取后端名称。</summary>
        string BackendName { get; }

        /// <summary>获取当前激活场景。</summary>
        SceneHandle ActiveScene { get; }

        /// <summary>加载场景并报告完成、进度和挂起事件。</summary>
        ISceneLoadOperation LoadSceneAsync(
            SceneLoadRequest request,
            Action<SceneLoadResult> onComplete,
            Action<float> onProgress,
            Action onSuspended);

        /// <summary>卸载场景。</summary>
        void UnloadSceneAsync(SceneHandle scene, Action onComplete);

        /// <summary>设置激活场景。</summary>
        void SetActiveScene(SceneHandle scene);

        /// <summary>获取激活场景；默认实现等价于 <see cref="ActiveScene"/>。</summary>
        SceneHandle GetActiveScene() => ActiveScene;

        /// <summary>卸载未使用资源。</summary>
        void UnloadUnusedAssets(Action onComplete);
    }
}
