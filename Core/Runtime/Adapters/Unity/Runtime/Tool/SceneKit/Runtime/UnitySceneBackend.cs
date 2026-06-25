#if !GODOT
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YokiFrame.Unity
{
    public sealed class UnitySceneBackend : IResSceneBackend
    {
        public string BackendName
        {
            get { return "Unity.SceneManager"; }
        }

        public ResSceneHandle ActiveScene
        {
            get { return ToSceneHandle(SceneManager.GetActiveScene()); }
        }

        public IResSceneLoadOperation LoadSceneAsync(
            ResSceneLoadRequest request,
            Action<ResSceneLoadResult> onComplete,
            Action<float> onProgress,
            Action onSuspended)
        {
            var loadMode = request.Mode == ResSceneLoadMode.Single ? LoadSceneMode.Single : LoadSceneMode.Additive;
            AsyncOperation operation;
            if (request.BuildIndex >= 0)
                operation = SceneManager.LoadSceneAsync(request.BuildIndex, loadMode);
            else
                operation = SceneManager.LoadSceneAsync(request.SceneName, loadMode);

            var loadOperation = new UnitySceneLoadOperation(operation);
            if (operation == null)
            {
                if (onComplete != null)
                    onComplete(new ResSceneLoadResult(new ResSceneHandle(request.SceneName, request.BuildIndex, false)));
                return loadOperation;
            }

            if (request.SuspendAtProgress < 1f)
            {
                operation.allowSceneActivation = false;
                if (onSuspended != null)
                    onSuspended();
            }

            if (onProgress != null)
                onProgress(operation.progress);

            operation.completed += _ =>
            {
                var scene = request.BuildIndex >= 0
                    ? SceneManager.GetSceneByBuildIndex(request.BuildIndex)
                    : SceneManager.GetSceneByName(request.SceneName);
                if (onComplete != null)
                    onComplete(new ResSceneLoadResult(ToSceneHandle(scene)));
            };

            return loadOperation;
        }

        public void UnloadSceneAsync(ResSceneHandle scene, Action onComplete)
        {
            var unityScene = ResolveScene(scene);
            if (!unityScene.IsValid())
            {
                if (onComplete != null)
                    onComplete();
                return;
            }

            var operation = SceneManager.UnloadSceneAsync(unityScene);
            if (operation == null)
            {
                if (onComplete != null)
                    onComplete();
                return;
            }

            operation.completed += _ =>
            {
                if (onComplete != null)
                    onComplete();
            };
        }

        public void SetActiveScene(ResSceneHandle scene)
        {
            var unityScene = ResolveScene(scene);
            if (unityScene.IsValid())
                SceneManager.SetActiveScene(unityScene);
        }

        public ResSceneHandle GetActiveScene()
        {
            return ActiveScene;
        }

        public void UnloadUnusedAssets(Action onComplete)
        {
            var operation = Resources.UnloadUnusedAssets();
            if (operation == null)
            {
                if (onComplete != null)
                    onComplete();
                return;
            }

            operation.completed += _ =>
            {
                if (onComplete != null)
                    onComplete();
            };
        }

        private static UnityEngine.SceneManagement.Scene ResolveScene(ResSceneHandle scene)
        {
            if (scene.BuildIndex >= 0)
            {
                var byIndex = SceneManager.GetSceneByBuildIndex(scene.BuildIndex);
                if (byIndex.IsValid())
                    return byIndex;
            }

            return string.IsNullOrEmpty(scene.SceneName)
                ? default(UnityEngine.SceneManagement.Scene)
                : SceneManager.GetSceneByName(scene.SceneName);
        }

        private static ResSceneHandle ToSceneHandle(UnityEngine.SceneManagement.Scene scene)
        {
            return new ResSceneHandle(scene.name, scene.buildIndex, scene.IsValid());
        }
    }
}
#endif
