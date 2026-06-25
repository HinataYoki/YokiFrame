using System;

namespace YokiFrame
{
    /// <summary>
    /// 定义 ResKit 统一管理的场景加载、卸载和激活能力。
    /// </summary>
    public interface IResSceneBackend
    {
        string BackendName { get; }
        ResSceneHandle ActiveScene { get; }

        IResSceneLoadOperation LoadSceneAsync(
            ResSceneLoadRequest request,
            Action<ResSceneLoadResult> onComplete,
            Action<float> onProgress,
            Action onSuspended);

        void UnloadSceneAsync(ResSceneHandle scene, Action onComplete);
        void SetActiveScene(ResSceneHandle scene);
        ResSceneHandle GetActiveScene();
        void UnloadUnusedAssets(Action onComplete);
    }
}
