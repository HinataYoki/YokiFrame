#if !GODOT
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using YokiFrame;

namespace YokiFrame.Unity
{
    /// <summary>
    /// IResourceProvider 的 Unity 实现，基于 Resources.Load
    /// 未来可替换为 YooAsset / Addressables 实现
    /// </summary>
    public sealed class UnityResourceProvider : IResourceProvider, IRawResourceProvider, IResSceneBackend
    {
        private readonly UnitySceneBackend mSceneBackend = new UnitySceneBackend();

        /// <summary>
        /// 获取资源提供者名称。
        /// </summary>
        public string ProviderName => "Unity.Resources";

        /// <summary>
        /// 获取场景加载后端名称。
        /// </summary>
        public string BackendName
        {
            get { return mSceneBackend.BackendName; }
        }

        /// <summary>
        /// 获取当前激活场景。
        /// </summary>
        public ResSceneHandle ActiveScene
        {
            get { return mSceneBackend.ActiveScene; }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 获取当前异步资源加载后端名称。
        /// </summary>
        public static string AsyncBackendName => "UniTask";
#else
        /// <summary>
        /// 获取当前异步资源加载后端名称。
        /// </summary>
        public static string AsyncBackendName => "TaskCompletionSource";
#endif

        /// <summary>
        /// 同步加载资源。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <returns>加载到的资源；不存在或类型不匹配时返回 null。</returns>
        public T Load<T>(string path) where T : class
        {
            var obj = Resources.Load(path);
            if (obj == default)
                return null;

            if (typeof(T) == typeof(GameObject))
                return obj as T;

            if (typeof(T) == typeof(IEngineObject))
            {
                var go = obj as GameObject;
                if (go != default)
                    return new UnityEngineObject(go) as T;
            }

            // 对于其他类型（Texture2D, Sprite, AudioClip 等），直接返回
            if (obj is T typedObj)
                return typedObj;

            return null;
        }

        /// <summary>
        /// 异步加载资源。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <returns>加载到的资源；不存在或类型不匹配时返回 null。</returns>
#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#else
        public async Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#endif
        {
#if YOKIFRAME_UNITASK_SUPPORT
            var obj = await LoadObjectAsync(path, token);
#else
            var obj = await LoadObjectAsync(path, token).ConfigureAwait(false);
#endif
            if (obj == default)
                return null;

            return ConvertResource<T>(obj);
        }

        /// <summary>
        /// 实例化资源路径对应的预制体。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <returns>实例化后的引擎对象；加载失败时返回 null。</returns>
        public IEngineObject Instantiate(string path)
        {
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == default)
                return null;

            var instance = UnityEngine.Object.Instantiate(prefab);
            return new UnityEngineObject(instance);
        }

        /// <summary>
        /// 同步加载原始二进制资源。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <returns>资源二进制内容；加载失败时返回 null。</returns>
        public byte[] LoadRaw(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            return asset != null ? asset.bytes : null;
        }

        /// <summary>
        /// 同步加载原始文本资源。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <returns>资源文本内容；加载失败时返回 null。</returns>
        public string LoadRawText(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            return asset != null ? asset.text : null;
        }

        /// <summary>
        /// 异步加载原始二进制资源。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源二进制内容；加载失败时返回 null。</returns>
#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<byte[]> LoadRawAsync(string path, CancellationToken token = default)
#else
        public async Task<byte[]> LoadRawAsync(string path, CancellationToken token = default)
#endif
        {
#if YOKIFRAME_UNITASK_SUPPORT
            var asset = await LoadTextAssetAsync(path, token);
#else
            var asset = await LoadTextAssetAsync(path, token).ConfigureAwait(false);
#endif
            return asset != null ? asset.bytes : null;
        }

        /// <summary>
        /// 异步加载原始文本资源。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源文本内容；加载失败时返回 null。</returns>
#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<string> LoadRawTextAsync(string path, CancellationToken token = default)
#else
        public async Task<string> LoadRawTextAsync(string path, CancellationToken token = default)
#endif
        {
#if YOKIFRAME_UNITASK_SUPPORT
            var asset = await LoadTextAssetAsync(path, token);
#else
            var asset = await LoadTextAssetAsync(path, token).ConfigureAwait(false);
#endif
            return asset != null ? asset.text : null;
        }

        /// <summary>
        /// 获取原始文件路径。Resources 后端不暴露真实文件路径。
        /// </summary>
        /// <param name="path">Resources 路径。</param>
        /// <returns>始终返回 null。</returns>
        public string GetRawFilePath(string path)
        {
            return null;
        }

        /// <summary>
        /// 通过 Unity SceneManager 加载场景。
        /// </summary>
        public IResSceneLoadOperation LoadSceneAsync(
            ResSceneLoadRequest request,
            Action<ResSceneLoadResult> onComplete,
            Action<float> onProgress,
            Action onSuspended)
        {
            return mSceneBackend.LoadSceneAsync(request, onComplete, onProgress, onSuspended);
        }

        /// <summary>
        /// 通过 Unity SceneManager 卸载场景。
        /// </summary>
        public void UnloadSceneAsync(ResSceneHandle scene, Action onComplete)
        {
            mSceneBackend.UnloadSceneAsync(scene, onComplete);
        }

        /// <summary>
        /// 设置当前激活场景。
        /// </summary>
        public void SetActiveScene(ResSceneHandle scene)
        {
            mSceneBackend.SetActiveScene(scene);
        }

        /// <summary>
        /// 获取当前激活场景。
        /// </summary>
        public ResSceneHandle GetActiveScene()
        {
            return mSceneBackend.GetActiveScene();
        }

        /// <summary>
        /// 卸载未使用资源。
        /// </summary>
        public void UnloadUnusedAssets(Action onComplete)
        {
            mSceneBackend.UnloadUnusedAssets(onComplete);
        }

        /// <summary>
        /// 释放资源引用。Resources 系统不支持手动释放，此方法保留统一接口形状。
        /// </summary>
        /// <param name="asset">要释放的资源对象。</param>
        public void Release(object asset)
        {
            // Resources 系统由引擎管理生命周期；YooAsset 适配器会在此释放 AssetHandle。
            if (asset is UnityEngineObject uobj && uobj.GameObject != default)
            {
                // 仅对实例化对象执行 Destroy
            }
        }

        private static T ConvertResource<T>(UnityEngine.Object obj) where T : class
        {
            if (typeof(T) == typeof(GameObject))
                return obj as T;

            if (typeof(T) == typeof(IEngineObject))
            {
                var go = obj as GameObject;
                if (go != default)
                    return new UnityEngineObject(go) as T;
            }

            return obj as T;
        }

        private sealed class UnitySceneBackend : IResSceneBackend
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

                var loadOperation = new UnityResourceSceneLoadOperation(operation);
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

            private sealed class UnityResourceSceneLoadOperation : IResSceneLoadOperation
            {
                private AsyncOperation mOperation;

                public UnityResourceSceneLoadOperation(AsyncOperation operation)
                {
                    mOperation = operation;
                }

                public bool IsSuspended
                {
                    get { return mOperation != null && !mOperation.allowSceneActivation; }
                }

                public float Progress
                {
                    get { return mOperation != null ? mOperation.progress : 0f; }
                }

                public void SuspendLoad()
                {
                    if (mOperation != null)
                        mOperation.allowSceneActivation = false;
                }

                public void ResumeLoad()
                {
                    if (mOperation != null)
                        mOperation.allowSceneActivation = true;
                }

                public void Recycle()
                {
                    mOperation = null;
                }
            }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        private static async UniTask<TextAsset> LoadTextAssetAsync(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var request = Resources.LoadAsync<TextAsset>(path);
            await request.ToUniTask(cancellationToken: token);
            return request.asset as TextAsset;
        }

        private static async UniTask<UnityEngine.Object> LoadObjectAsync(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var request = Resources.LoadAsync(path);
            await request.ToUniTask(cancellationToken: token);
            return request.asset;
        }
#else
        private static async Task<TextAsset> LoadTextAssetAsync(string path, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                token.ThrowIfCancellationRequested();

            var request = Resources.LoadAsync<TextAsset>(path);
            var tcs = new TaskCompletionSource<TextAsset>();
            var registration = token.Register(() =>
            {
                tcs.TrySetCanceled(token);
            });

            try
            {
                request.completed += _ =>
                {
                    if (token.IsCancellationRequested)
                        tcs.TrySetCanceled(token);
                    else
                        tcs.TrySetResult(request.asset as TextAsset);
                };

                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                registration.Dispose();
            }
        }

        private static async Task<UnityEngine.Object> LoadObjectAsync(string path, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                token.ThrowIfCancellationRequested();

            var request = Resources.LoadAsync(path);
            var tcs = new TaskCompletionSource<ResourceRequest>();
            var registration = token.Register(() =>
            {
                tcs.TrySetCanceled(token);
            });

            try
            {
                request.completed += _ =>
                {
                    if (token.IsCancellationRequested)
                        tcs.TrySetCanceled(token);
                    else
                        tcs.TrySetResult(request);
                };

                await tcs.Task.ConfigureAwait(false);

                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();

                return request.asset;
            }
            finally
            {
                registration.Dispose();
            }
        }
#endif
    }
}
#endif
