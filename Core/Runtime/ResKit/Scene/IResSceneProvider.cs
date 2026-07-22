using System;

namespace YokiFrame
{
    /// <summary>定义资源 Provider 可选的场景加载能力。</summary>
    public interface IResSceneProvider
    {
        /// <summary>获取当前场景后端名称。</summary>
        string SceneBackendName { get; }

        /// <summary>获取当前激活场景。</summary>
        ResSceneHandle ActiveScene { get; }

        /// <summary>加载场景并通过回调报告结果、进度和可挂起状态。</summary>
        IResSceneLoadOperation LoadSceneAsync(
            ResSceneLoadRequest request,
            Action<ResSceneLoadResult> onComplete,
            Action<float> onProgress,
            Action onSuspended);

        /// <summary>异步卸载指定场景。</summary>
        void UnloadSceneAsync(ResSceneHandle scene, Action onComplete);

        /// <summary>设置当前激活场景。</summary>
        void SetActiveScene(ResSceneHandle scene);

        /// <summary>获取当前激活场景。</summary>
        ResSceneHandle GetActiveScene();

        /// <summary>请求宿主清理未使用资源。</summary>
        void UnloadUnusedAssets(Action onComplete);
    }
}
