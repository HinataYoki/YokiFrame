using System;

namespace YokiFrame
{
    /// <summary>把当前 ResKit Provider 的场景能力映射为 SceneKit 后端。</summary>
    internal sealed class ResKitSceneBackendAdapter : ISceneBackend
    {
        private readonly IResSceneProvider mProvider;

        /// <summary>创建绑定指定 ResKit 场景 Provider 的适配器。</summary>
        /// <param name="provider">当前资源 Provider 的场景能力。</param>
        internal ResKitSceneBackendAdapter(IResSceneProvider provider)
        {
            mProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <inheritdoc />
        public string BackendName => "ResKit:" + mProvider.SceneBackendName;

        /// <inheritdoc />
        public SceneHandle ActiveScene => ToSceneHandle(mProvider.ActiveScene);

        /// <inheritdoc />
        public ISceneLoadOperation LoadSceneAsync(
            SceneLoadRequest request,
            Action<SceneLoadResult> onComplete,
            Action<float> onProgress,
            Action onSuspended)
        {
            ResSceneLoadRequest resRequest = new(
                request.SceneName,
                request.BuildIndex,
                request.Mode == SceneLoadMode.Single ? ResSceneLoadMode.Single : ResSceneLoadMode.Additive,
                request.SuspendAtProgress,
                request.Data,
                request.IsPreload);
            IResSceneLoadOperation operation = mProvider.LoadSceneAsync(
                resRequest,
                result => onComplete?.Invoke(new SceneLoadResult(ToSceneHandle(result.Scene))),
                onProgress,
                onSuspended);
            return new ResSceneLoadOperationAdapter(operation);
        }

        /// <inheritdoc />
        public void UnloadSceneAsync(SceneHandle scene, Action onComplete)
        {
            mProvider.UnloadSceneAsync(ToResSceneHandle(scene), onComplete);
        }

        /// <inheritdoc />
        public void SetActiveScene(SceneHandle scene)
        {
            mProvider.SetActiveScene(ToResSceneHandle(scene));
        }

        /// <inheritdoc />
        public SceneHandle GetActiveScene()
        {
            return ToSceneHandle(mProvider.GetActiveScene());
        }

        /// <inheritdoc />
        public void UnloadUnusedAssets(Action onComplete)
        {
            mProvider.UnloadUnusedAssets(onComplete);
        }

        /// <summary>把 ResKit 场景句柄转换为 SceneKit 句柄。</summary>
        private static SceneHandle ToSceneHandle(ResSceneHandle scene)
        {
            return new SceneHandle(scene.SceneName, scene.BuildIndex, scene.IsValid);
        }

        /// <summary>把 SceneKit 场景句柄转换为 ResKit 场景句柄。</summary>
        private static ResSceneHandle ToResSceneHandle(SceneHandle scene)
        {
            return new ResSceneHandle(scene.SceneName, scene.BuildIndex, scene.IsValid);
        }
    }
}
