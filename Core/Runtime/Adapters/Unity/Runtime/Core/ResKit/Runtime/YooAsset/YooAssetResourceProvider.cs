#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections.Generic;
using System.Threading;
using YooAsset;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using YokiFrame;
using Object = UnityEngine.Object;
using YooSceneHandle = YooAsset.SceneHandle;

namespace YokiFrame.Unity
{
    /// <summary>
    /// ResKit 的 YooAsset 后端。对外仍通过 YokiFrame.ResKit 统一访问。
    /// </summary>
    public sealed class YooAssetResourceProvider : IResourceProvider, IRawResourceProvider, IResSceneBackend
    {
        private const string DEFAULT_PACKAGE_NAME = "DefaultPackage";

        private readonly object mHandleLock = new();
        private readonly Dictionary<object, Stack<AssetHandle>> mHandlesByAsset = new();
        private readonly Dictionary<string, YooSceneHandle> mSceneHandlesByName = new Dictionary<string, YooSceneHandle>(StringComparer.Ordinal);
        private readonly IYooAssetResourceBackend mResourceBackend;
        private readonly IYooAssetRawFileBackend mRawFileBackend;
        private readonly IYooAssetSceneBackend mSceneBackend;
        private ResSceneHandle mActiveScene;

        /// <summary>
        /// 获取资源提供者名称。
        /// </summary>
        public string ProviderName => "YooAsset";

        /// <summary>
        /// 获取场景加载后端名称。
        /// </summary>
        public string BackendName => "YooAsset.Scene";

        /// <summary>
        /// 获取当前激活场景。
        /// </summary>
        public ResSceneHandle ActiveScene
        {
            get { return mActiveScene; }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 获取当前异步后端名称。
        /// </summary>
        public static string AsyncBackendName => "UniTask";
#else
        /// <summary>
        /// 获取当前异步后端名称。
        /// </summary>
        public static string AsyncBackendName => "Task";
#endif

        /// <summary>
        /// 使用默认资源包创建 YooAsset 资源提供者。
        /// </summary>
        public YooAssetResourceProvider()
            : this(CreateDefaultResourceBackend(), CreateDefaultRawFileBackend())
        {
        }

#if YOOASSET_3_0_OR_NEWER
        /// <summary>
        /// 使用指定 YooAsset 资源包创建资源提供者。
        /// </summary>
        /// <param name="package">YooAsset 资源包实例。</param>
        public YooAssetResourceProvider(ResourcePackage package)
            : this(new YooAssetV3ResourceBackend(package), new YooAssetV3RawFileBackend(package), new YooAssetV3SceneBackend(package))
        {
        }

        /// <summary>
        /// 使用指定资源包名称创建资源提供者。
        /// </summary>
        /// <param name="packageName">YooAsset 资源包名称。</param>
        public YooAssetResourceProvider(string packageName)
            : this(new YooAssetV3ResourceBackend(packageName), new YooAssetV3RawFileBackend(packageName), new YooAssetV3SceneBackend(packageName))
        {
        }
#else
        /// <summary>
        /// 使用指定资源包名称创建资源提供者。
        /// </summary>
        /// <param name="packageName">YooAsset 资源包名称。</param>
        public YooAssetResourceProvider(string packageName)
            : this()
        {
        }
#endif

        internal YooAssetResourceProvider(IYooAssetResourceBackend resourceBackend, IYooAssetRawFileBackend rawFileBackend)
            : this(resourceBackend, rawFileBackend, CreateDefaultSceneBackend())
        {
        }

        internal YooAssetResourceProvider(
            IYooAssetResourceBackend resourceBackend,
            IYooAssetRawFileBackend rawFileBackend,
            IYooAssetSceneBackend sceneBackend)
        {
            mResourceBackend = resourceBackend ?? throw new ArgumentNullException(nameof(resourceBackend));
            mRawFileBackend = rawFileBackend ?? throw new ArgumentNullException(nameof(rawFileBackend));
            mSceneBackend = sceneBackend ?? throw new ArgumentNullException(nameof(sceneBackend));
        }

        /// <inheritdoc />
        public T Load<T>(string path) where T : class
        {
            var unityType = ResolveUnityAssetType<T>();
            var handle = LoadAssetHandle(path, unityType);
            var asset = ConvertAsset<T>(handle != null ? handle.AssetObject : null);
            if (asset == null)
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
                return null;
            }

            RegisterHandle(asset, handle);
            return asset;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <inheritdoc />
        public async UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#else
        /// <inheritdoc />
        public async Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#endif
        {
            var unityType = ResolveUnityAssetType<T>();
#if YOKIFRAME_UNITASK_SUPPORT
            var handle = await LoadAssetHandleAsync(path, unityType, token);
#else
            var handle = await LoadAssetHandleAsync(path, unityType, token).ConfigureAwait(false);
#endif
            var asset = ConvertAsset<T>(handle != null ? handle.AssetObject : null);
            if (asset == null)
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
                return null;
            }

            RegisterHandle(asset, handle);
            return asset;
        }

        /// <inheritdoc />
        public IEngineObject Instantiate(string path)
        {
            var handle = LoadAssetHandle(path, typeof(GameObject));
            var prefab = handle != null ? handle.AssetObject as GameObject : null;
            if (prefab == default)
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
                return null;
            }

            var instance = Object.Instantiate(prefab);
            YooAssetHandleAwaiter.ReleaseQuietly(handle);
            return new UnityEngineObject(instance);
        }

        /// <inheritdoc />
        public void Release(object asset)
        {
            if (asset == null)
                return;

            var handle = TakeHandle(asset);
            if (handle != null)
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
                return;
            }

            var engineObject = asset as UnityEngineObject;
            if (engineObject != null && engineObject.GameObject != default)
            {
                Object.Destroy(engineObject.GameObject);
                return;
            }
        }

        /// <inheritdoc />
        public byte[] LoadRaw(string path)
        {
            return mRawFileBackend.LoadRaw(path);
        }

        /// <inheritdoc />
        public string LoadRawText(string path)
        {
            return mRawFileBackend.LoadRawText(path);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <inheritdoc />
        public async UniTask<byte[]> LoadRawAsync(string path, CancellationToken token = default)
#else
        /// <inheritdoc />
        public async Task<byte[]> LoadRawAsync(string path, CancellationToken token = default)
#endif
        {
#if YOKIFRAME_UNITASK_SUPPORT
            return await mRawFileBackend.LoadRawAsync(path, token);
#else
            return await mRawFileBackend.LoadRawAsync(path, token).ConfigureAwait(false);
#endif
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <inheritdoc />
        public async UniTask<string> LoadRawTextAsync(string path, CancellationToken token = default)
#else
        /// <inheritdoc />
        public async Task<string> LoadRawTextAsync(string path, CancellationToken token = default)
#endif
        {
#if YOKIFRAME_UNITASK_SUPPORT
            return await mRawFileBackend.LoadRawTextAsync(path, token);
#else
            return await mRawFileBackend.LoadRawTextAsync(path, token).ConfigureAwait(false);
#endif
        }

        /// <inheritdoc />
        public string GetRawFilePath(string path)
        {
            return mRawFileBackend.GetRawFilePath(path);
        }

        /// <inheritdoc />
        public IResSceneLoadOperation LoadSceneAsync(
            ResSceneLoadRequest request,
            Action<ResSceneLoadResult> onComplete,
            Action<float> onProgress,
            Action onSuspended)
        {
            if (request.BuildIndex >= 0)
            {
                var invalidHandle = new ResSceneHandle(request.SceneName, request.BuildIndex, false);
                if (onComplete != null)
                    onComplete(new ResSceneLoadResult(invalidHandle));
                return new YooAssetSceneLoadOperation(null);
            }

            if (string.IsNullOrEmpty(request.SceneName))
            {
                var invalidHandle = new ResSceneHandle(request.SceneName, request.BuildIndex, false);
                if (onComplete != null)
                    onComplete(new ResSceneLoadResult(invalidHandle));
                return new YooAssetSceneLoadOperation(null);
            }

            var sceneMode = request.Mode == ResSceneLoadMode.Single ? LoadSceneMode.Single : LoadSceneMode.Additive;
            var allowSceneActivation = request.SuspendAtProgress >= 1f;
            var sceneHandle = mSceneBackend.LoadSceneAsync(request.SceneName, sceneMode, allowSceneActivation);
            if (!allowSceneActivation && onSuspended != null)
                onSuspended();

            if (onProgress != null)
                onProgress(sceneHandle != null ? sceneHandle.Progress : 0f);

            if (sceneHandle == null)
            {
                var invalidHandle = new ResSceneHandle(request.SceneName, request.BuildIndex, false);
                if (onComplete != null)
                    onComplete(new ResSceneLoadResult(invalidHandle));
                return new YooAssetSceneLoadOperation(null);
            }

            sceneHandle.Completed += handle =>
            {
                var scene = ToResSceneHandle(handle);
                mSceneHandlesByName[scene.SceneName] = handle;
                if (request.Mode == ResSceneLoadMode.Single || !mActiveScene.IsValid)
                    mActiveScene = scene;
                if (onProgress != null)
                    onProgress(handle.Progress);
                if (onComplete != null)
                    onComplete(new ResSceneLoadResult(scene));
            };

            return new YooAssetSceneLoadOperation(sceneHandle);
        }

        /// <inheritdoc />
        public void UnloadSceneAsync(ResSceneHandle scene, Action onComplete)
        {
            if (string.IsNullOrEmpty(scene.SceneName))
            {
                if (onComplete != null)
                    onComplete();
                return;
            }

            YooSceneHandle handle;
            if (!mSceneHandlesByName.TryGetValue(scene.SceneName, out handle) || handle == null)
            {
                if (onComplete != null)
                    onComplete();
                return;
            }

            mSceneHandlesByName.Remove(scene.SceneName);
            if (mActiveScene.SceneName == scene.SceneName)
                mActiveScene = default(ResSceneHandle);

#if YOOASSET_3_0_OR_NEWER
            var unloadOperation = handle.UnloadSceneAsync();
#else
            var unloadOperation = handle.UnloadAsync();
#endif
            unloadOperation.Completed += _ =>
            {
                if (onComplete != null)
                    onComplete();
            };
        }

        /// <inheritdoc />
        public void SetActiveScene(ResSceneHandle scene)
        {
            YooSceneHandle handle;
            if (!string.IsNullOrEmpty(scene.SceneName) &&
                mSceneHandlesByName.TryGetValue(scene.SceneName, out handle) &&
                handle != null &&
                handle.ActivateScene())
            {
                mActiveScene = ToResSceneHandle(handle);
            }
        }

        /// <inheritdoc />
        public ResSceneHandle GetActiveScene()
        {
            return mActiveScene;
        }

        /// <inheritdoc />
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

        private AssetHandle LoadAssetHandle(string path, Type unityType)
        {
            return mResourceBackend.LoadAsset(path, unityType);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        private UniTask<AssetHandle> LoadAssetHandleAsync(string path, Type unityType, CancellationToken token)
#else
        private Task<AssetHandle> LoadAssetHandleAsync(string path, Type unityType, CancellationToken token)
#endif
        {
            return mResourceBackend.LoadAssetAsync(path, unityType, token);
        }

        private void RegisterHandle(object asset, AssetHandle handle)
        {
            if (asset == null || handle == null)
                return;

            lock (mHandleLock)
            {
                Stack<AssetHandle> handles;
                if (!mHandlesByAsset.TryGetValue(asset, out handles))
                {
                    handles = new();
                    mHandlesByAsset.Add(asset, handles);
                }

                handles.Push(handle);
            }
        }

        private AssetHandle TakeHandle(object asset)
        {
            lock (mHandleLock)
            {
                Stack<AssetHandle> handles;
                if (!mHandlesByAsset.TryGetValue(asset, out handles) || handles.Count == 0)
                    return null;

                var handle = handles.Pop();
                if (handles.Count == 0)
                    mHandlesByAsset.Remove(asset);

                return handle;
            }
        }

        private static Type ResolveUnityAssetType<T>() where T : class
        {
            if (typeof(T) == typeof(IEngineObject))
                return typeof(GameObject);

            if (typeof(Object).IsAssignableFrom(typeof(T)))
                return typeof(T);

            return typeof(Object);
        }

        private static T ConvertAsset<T>(Object asset) where T : class
        {
            if (asset == default)
                return null;

            if (typeof(T) == typeof(IEngineObject))
            {
                var gameObject = asset as GameObject;
                return gameObject != default ? new UnityEngineObject(gameObject) as T : null;
            }

            return asset as T;
        }

        private static IYooAssetResourceBackend CreateDefaultResourceBackend()
        {
#if YOOASSET_3_0_OR_NEWER
            return new YooAssetV3ResourceBackend(DEFAULT_PACKAGE_NAME);
#else
            return new YooAssetV2ResourceBackend();
#endif
        }

        private static IYooAssetRawFileBackend CreateDefaultRawFileBackend()
        {
#if YOOASSET_3_0_OR_NEWER
            return new YooAssetV3RawFileBackend(DEFAULT_PACKAGE_NAME);
#else
            return new YooAssetV2RawFileBackend();
#endif
        }

        private static IYooAssetSceneBackend CreateDefaultSceneBackend()
        {
#if YOOASSET_3_0_OR_NEWER
            return new YooAssetV3SceneBackend(DEFAULT_PACKAGE_NAME);
#else
            return new YooAssetV2SceneBackend();
#endif
        }

        private static ResSceneHandle ToResSceneHandle(YooSceneHandle handle)
        {
            if (handle == null || !handle.IsValid)
                return default(ResSceneHandle);

            var scene = handle.SceneObject;
            return new ResSceneHandle(handle.SceneName, scene.buildIndex, scene.IsValid());
        }

        internal interface IYooAssetSceneBackend
        {
            YooSceneHandle LoadSceneAsync(string path, LoadSceneMode sceneMode, bool allowSceneActivation);
        }

        private sealed class YooAssetSceneLoadOperation : IResSceneLoadOperation
        {
            private YooSceneHandle mHandle;

            public YooAssetSceneLoadOperation(YooSceneHandle handle)
            {
                mHandle = handle;
            }

            public bool IsSuspended { get; private set; }

            public float Progress
            {
                get { return mHandle != null ? mHandle.Progress : 0f; }
            }

            public void SuspendLoad()
            {
                IsSuspended = true;
            }

            public void ResumeLoad()
            {
                if (mHandle != null)
#if YOOASSET_3_0_OR_NEWER
                    mHandle.AllowSceneActivation();
#else
                    mHandle.UnSuspend();
#endif
                IsSuspended = false;
            }

            public void Recycle()
            {
                mHandle = null;
                IsSuspended = false;
            }
        }

#if YOOASSET_3_0_OR_NEWER
        private sealed class YooAssetV3SceneBackend : IYooAssetSceneBackend
        {
            private readonly ResourcePackage mPackage;
            private readonly string mPackageName;

            public YooAssetV3SceneBackend(ResourcePackage package)
            {
                mPackage = package ?? throw new ArgumentNullException(nameof(package));
            }

            public YooAssetV3SceneBackend(string packageName)
            {
                mPackageName = string.IsNullOrEmpty(packageName) ? DEFAULT_PACKAGE_NAME : packageName;
            }

            public YooSceneHandle LoadSceneAsync(string path, LoadSceneMode sceneMode, bool allowSceneActivation)
            {
                return ResolvePackage().LoadSceneAsync(path, sceneMode, LocalPhysicsMode.None, allowSceneActivation);
            }

            private ResourcePackage ResolvePackage()
            {
                if (mPackage != null)
                    return mPackage;

                return YooAssets.GetPackage(mPackageName);
            }
        }
#else
        private sealed class YooAssetV2SceneBackend : IYooAssetSceneBackend
        {
            public YooSceneHandle LoadSceneAsync(string path, LoadSceneMode sceneMode, bool allowSceneActivation)
            {
#if YOOASSET_3_0_OR_NEWER
                return YooAssets.LoadSceneAsync(path, sceneMode, LocalPhysicsMode.None, allowSceneActivation);
#else
                return YooAssets.LoadSceneAsync(path, sceneMode, LocalPhysicsMode.None, !allowSceneActivation);
#endif
            }
        }
#endif
    }
}
#endif
#endif
