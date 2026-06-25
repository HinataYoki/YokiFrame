using System;

namespace YokiFrame
{
    /// <summary>
    /// SceneKit 默认委托到 ResKit 场景后端，保留 SceneKit.SetBackend 作为显式覆盖点。
    /// </summary>
    internal sealed class ResKitSceneBackendAdapter : ISceneBackend
    {
        public string BackendName
        {
            get { return "ResKit:" + EnsureResKitBackend().BackendName; }
        }

        public SceneHandle ActiveScene
        {
            get { return ToSceneHandle(EnsureResKitBackend().ActiveScene); }
        }

        public ISceneLoadOperation LoadSceneAsync(
            SceneLoadRequest request,
            Action<SceneLoadResult> onComplete,
            Action<float> onProgress,
            Action onSuspended)
        {
            var resRequest = new ResSceneLoadRequest(
                request.SceneName,
                request.BuildIndex,
                ToResSceneLoadMode(request.Mode),
                request.SuspendAtProgress,
                request.Data,
                request.IsPreload);

            var operation = EnsureResKitBackend().LoadSceneAsync(
                resRequest,
                result =>
                {
                    if (onComplete != null)
                        onComplete(new SceneLoadResult(ToSceneHandle(result.Scene)));
                },
                onProgress,
                onSuspended);

            return new ResSceneLoadOperationAdapter(operation);
        }

        public void UnloadSceneAsync(SceneHandle scene, Action onComplete)
        {
            EnsureResKitBackend().UnloadSceneAsync(ToResSceneHandle(scene), onComplete);
        }

        public void SetActiveScene(SceneHandle scene)
        {
            EnsureResKitBackend().SetActiveScene(ToResSceneHandle(scene));
        }

        public SceneHandle GetActiveScene()
        {
            return ToSceneHandle(EnsureResKitBackend().GetActiveScene());
        }

        public void UnloadUnusedAssets(Action onComplete)
        {
            EnsureResKitBackend().UnloadUnusedAssets(onComplete);
        }

        private static IResSceneBackend EnsureResKitBackend()
        {
            var backend = ResKit.GetSceneBackend();
            if (backend == null)
                throw new InvalidOperationException("ResKit scene backend is not configured. Call ResKit.SetSceneBackend from an engine adapter first.");

            return backend;
        }

        private static ResSceneLoadMode ToResSceneLoadMode(SceneLoadMode mode)
        {
            return mode == SceneLoadMode.Single ? ResSceneLoadMode.Single : ResSceneLoadMode.Additive;
        }

        private static SceneHandle ToSceneHandle(ResSceneHandle scene)
        {
            return new SceneHandle(scene.SceneName, scene.BuildIndex, scene.IsValid);
        }

        private static ResSceneHandle ToResSceneHandle(SceneHandle scene)
        {
            return new ResSceneHandle(scene.SceneName, scene.BuildIndex, scene.IsValid);
        }
    }
}
