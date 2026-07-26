#if UNITY_2022_3_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YokiFrame.Unity
{
    /// <summary>把 Unity SceneManager 映射为 ResKit 的可选场景能力。</summary>
    internal sealed class UnitySceneProvider : IResSceneProvider
    {
        /// <summary>获取 Unity 场景后端名称。</summary>
        public string SceneBackendName => "Unity.SceneManager";

        /// <summary>获取 Unity 当前激活场景。</summary>
        public ResSceneHandle ActiveScene => ToHandle(SceneManager.GetActiveScene());

        /// <summary>创建 Unity 异步加载操作并订阅完成回调。</summary>
        public IResSceneLoadOperation LoadSceneAsync(
            ResSceneLoadRequest request,
            Action<ResSceneLoadResult> onComplete,
            Action<float> onProgress,
            Action onSuspended)
        {
            LoadSceneMode mode = request.Mode == ResSceneLoadMode.Single
                ? LoadSceneMode.Single
                : LoadSceneMode.Additive;
            AsyncOperation operation = request.BuildIndex >= 0
                ? SceneManager.LoadSceneAsync(request.BuildIndex, mode)
                : SceneManager.LoadSceneAsync(request.SceneName, mode);
            var sceneOperation = new UnitySceneLoadOperation(operation, request.SuspendAtProgress, onProgress, onSuspended);
            if (operation == null)
            {
                onComplete?.Invoke(new ResSceneLoadResult(new ResSceneHandle(
                    request.SceneName, request.BuildIndex, false)));
                return sceneOperation;
            }

            operation.completed += _ =>
            {
                try
                {
                    UnityEngine.SceneManagement.Scene scene = request.BuildIndex >= 0
                        ? SceneManager.GetSceneByBuildIndex(request.BuildIndex)
                        : SceneManager.GetSceneByName(request.SceneName);
                    onComplete?.Invoke(new ResSceneLoadResult(ToHandle(scene)));
                }
                finally
                {
                    // 用户完成回调异常不能让已结束的场景操作继续占用帧循环监听。
                    sceneOperation.MarkCompleted();
                }
            };
            sceneOperation.ReportInitialProgress();
            return sceneOperation;
        }

        /// <summary>解析并异步卸载 Unity 场景。</summary>
        public void UnloadSceneAsync(ResSceneHandle scene, Action onComplete)
        {
            UnityEngine.SceneManagement.Scene unityScene = ResolveScene(scene);
            if (!unityScene.IsValid())
            {
                onComplete?.Invoke();
                return;
            }

            AsyncOperation operation = SceneManager.UnloadSceneAsync(unityScene);
            if (operation == null)
            {
                onComplete?.Invoke();
                return;
            }

            operation.completed += _ => onComplete?.Invoke();
        }

        /// <summary>设置 Unity 当前激活场景。</summary>
        public void SetActiveScene(ResSceneHandle scene)
        {
            UnityEngine.SceneManagement.Scene unityScene = ResolveScene(scene);
            if (unityScene.IsValid())
            {
                SceneManager.SetActiveScene(unityScene);
            }
        }

        /// <summary>请求 Unity 卸载未使用资源。</summary>
        public void UnloadUnusedAssets(Action onComplete)
        {
            AsyncOperation operation = Resources.UnloadUnusedAssets();
            if (operation == null)
            {
                onComplete?.Invoke();
                return;
            }

            operation.completed += _ => onComplete?.Invoke();
        }

        /// <summary>按构建索引优先、名称回退解析 Unity 场景。</summary>
        private static UnityEngine.SceneManagement.Scene ResolveScene(ResSceneHandle scene)
        {
            if (scene.BuildIndex >= 0)
            {
                UnityEngine.SceneManagement.Scene byIndex = SceneManager.GetSceneByBuildIndex(scene.BuildIndex);
                if (byIndex.IsValid())
                {
                    return byIndex;
                }
            }

            return string.IsNullOrEmpty(scene.SceneName)
                ? default
                : SceneManager.GetSceneByName(scene.SceneName);
        }

        /// <summary>把 Unity 场景转换为 ResKit 句柄。</summary>
        private static ResSceneHandle ToHandle(UnityEngine.SceneManagement.Scene scene)
        {
            return new ResSceneHandle(scene.name, scene.buildIndex, scene.IsValid());
        }
    }
}
#endif
