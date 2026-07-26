#if UNITY_2022_3_OR_NEWER
using System;
using System.Threading;
#if !YOKIFRAME_UNITASK_SUPPORT
using System.Threading.Tasks;
#endif
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 基于 Unity Resources 的默认资源后端，负责把宿主资源 API 映射到 ResKit 契约。
    /// </summary>
    public sealed class UnityResourceProvider :
        IResourceProvider,
        IRawResourceProvider,
        IResSceneProvider,
        IResourceProviderCapabilities
    {
#if UNITY_EDITOR
        private const string PROVIDER_NAME = "Unity.Resources";
#endif

        private readonly UnitySceneProvider mSceneProvider = new();

        /// <summary>
        /// 创建一个无状态的 Unity Resources Provider。
        /// </summary>
        public UnityResourceProvider()
        {
        }

#if UNITY_EDITOR
        /// <summary>
        /// 获取用于诊断和 Workbench 展示的稳定 Provider 名称。
        /// </summary>
        public string ProviderName => PROVIDER_NAME;
#endif

        /// <summary>获取 Resources TextAsset 支持 raw bytes。</summary>
        public bool SupportsRawBytes => true;

        /// <summary>获取 Resources TextAsset 支持 raw 文本。</summary>
        public bool SupportsRawText => true;

        /// <summary>获取 Unity SceneManager 场景后端名称。</summary>
        public string SceneBackendName => mSceneProvider.SceneBackendName;

        /// <summary>获取 Unity 当前激活场景。</summary>
        public ResSceneHandle ActiveScene => mSceneProvider.ActiveScene;

        /// <summary>
        /// 从 Resources 同步加载指定类型的资源；资源不存在或类型不匹配时返回空。
        /// </summary>
        /// <typeparam name="T">调用方期望的资源类型。</typeparam>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <returns>匹配的资源对象；资源不存在或类型不匹配时返回空。</returns>
        public T Load<T>(string path) where T : class
        {
            var asset = LoadUnityObject(path, typeof(T));
            return asset as T;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 从 Resources 异步加载指定类型的资源；取消只结束本次等待，Unity 请求由引擎自行完成。
        /// </summary>
        /// <typeparam name="T">调用方期望的资源类型。</typeparam>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <param name="token">本次等待使用的取消令牌。</param>
        /// <returns>匹配的资源对象；资源不存在或类型不匹配时返回空。</returns>
        public async UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
        {
            var asset = await LoadUnityObjectUniTaskAsync(path, typeof(T), token);
            return asset as T;
        }
#else
        /// <summary>
        /// 从 Resources 异步加载指定类型的资源；取消只结束本次等待，Unity 请求由引擎自行完成。
        /// </summary>
        /// <typeparam name="T">调用方期望的资源类型。</typeparam>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <param name="token">本次等待使用的取消令牌。</param>
        /// <returns>匹配的资源对象；资源不存在或类型不匹配时返回空。</returns>
        public Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
        {
            return LoadResourceTaskAsync<T>(path, token);
        }
#endif

        /// <summary>
        /// 读取 TextAsset 的二进制内容，不把临时 TextAsset 注册到 ResKit 对象缓存。
        /// </summary>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <returns>资源二进制内容；资源不存在时返回空。</returns>
        public byte[] LoadRaw(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            return asset != default ? asset.bytes : null;
        }

        /// <summary>
        /// 读取 TextAsset 的文本内容，不把临时 TextAsset 注册到 ResKit 对象缓存。
        /// </summary>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <returns>资源文本内容；资源不存在时返回空。</returns>
        public string LoadRawText(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            return asset != default ? asset.text : null;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步读取 TextAsset 的二进制内容；取消只结束本次等待。
        /// </summary>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <param name="token">本次等待使用的取消令牌。</param>
        /// <returns>资源二进制内容；资源不存在时返回空。</returns>
        public async UniTask<byte[]> LoadRawAsync(string path, CancellationToken token = default)
        {
            var asset = await LoadTextAssetUniTaskAsync(path, token);
            return asset != default ? asset.bytes : null;
        }

        /// <summary>
        /// 异步读取 TextAsset 的文本内容；取消只结束本次等待。
        /// </summary>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <param name="token">本次等待使用的取消令牌。</param>
        /// <returns>资源文本内容；资源不存在时返回空。</returns>
        public async UniTask<string> LoadRawTextAsync(string path, CancellationToken token = default)
        {
            var asset = await LoadTextAssetUniTaskAsync(path, token);
            return asset != default ? asset.text : null;
        }
#else
        /// <summary>
        /// 异步读取 TextAsset 的二进制内容；取消只结束本次等待。
        /// </summary>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <param name="token">本次等待使用的取消令牌。</param>
        /// <returns>资源二进制内容；资源不存在时返回空。</returns>
        public Task<byte[]> LoadRawAsync(string path, CancellationToken token = default)
        {
            return LoadRawBytesTaskAsync(path, token);
        }

        /// <summary>
        /// 异步读取 TextAsset 的文本内容；取消只结束本次等待。
        /// </summary>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <param name="token">本次等待使用的取消令牌。</param>
        /// <returns>资源文本内容；资源不存在时返回空。</returns>
        public Task<string> LoadRawTextAsync(string path, CancellationToken token = default)
        {
            return LoadRawTextTaskAsync(path, token);
        }
#endif

        /// <summary>
        /// 释放 ResKit 对资源的托管引用；Resources 底层对象继续由 Unity 生命周期统一管理。
        /// </summary>
        /// <param name="asset">当前 Provider 曾返回的资源对象。</param>
        public void Release(object asset)
        {
            // Resources 可能与框架外调用共享同一底层对象，主动 UnloadAsset 会使其它持有者失效。
        }

        /// <summary>通过 Unity SceneManager 加载场景。</summary>
        public IResSceneLoadOperation LoadSceneAsync(
            ResSceneLoadRequest request,
            Action<ResSceneLoadResult> onComplete,
            Action<float> onProgress,
            Action onSuspended)
        {
            return mSceneProvider.LoadSceneAsync(request, onComplete, onProgress, onSuspended);
        }

        /// <summary>通过 Unity SceneManager 卸载场景。</summary>
        public void UnloadSceneAsync(ResSceneHandle scene, Action onComplete)
        {
            mSceneProvider.UnloadSceneAsync(scene, onComplete);
        }

        /// <summary>设置 Unity 当前激活场景。</summary>
        public void SetActiveScene(ResSceneHandle scene)
        {
            mSceneProvider.SetActiveScene(scene);
        }

        /// <summary>请求 Unity 卸载未使用资源。</summary>
        public void UnloadUnusedAssets(Action onComplete)
        {
            mSceneProvider.UnloadUnusedAssets(onComplete);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 使用 UniTask 原生等待 Unity ResourceRequest，避免 Task 桥接产生额外状态对象。
        /// </summary>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <param name="requestedType">调用方期望的资源类型。</param>
        /// <param name="token">本次等待使用的取消令牌。</param>
        /// <returns>Unity 返回的资源对象；资源不存在时返回空。</returns>
        private static async UniTask<Object> LoadUnityObjectUniTaskAsync(
            string path,
            Type requestedType,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var resourceType = ResolveResourceType(requestedType);
            var request = resourceType != null
                ? Resources.LoadAsync(path, resourceType)
                : Resources.LoadAsync(path);
            await request.ToUniTask(cancellationToken: token);
            return request.asset;
        }

        /// <summary>
        /// 复用 UniTask ResourceRequest 等待逻辑加载 TextAsset。
        /// </summary>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <param name="token">本次等待使用的取消令牌。</param>
        /// <returns>加载到的 TextAsset；资源不存在时返回空。</returns>
        private static async UniTask<TextAsset> LoadTextAssetUniTaskAsync(
            string path,
            CancellationToken token)
        {
            var asset = await LoadUnityObjectUniTaskAsync(path, typeof(TextAsset), token);
            return asset as TextAsset;
        }
#else
        /// <summary>
        /// 等待 Unity ResourceRequest，并把返回对象转换为调用方期望的类型。
        /// </summary>
        /// <typeparam name="T">调用方期望的资源类型。</typeparam>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <param name="token">本次等待使用的取消令牌。</param>
        /// <returns>匹配的资源对象；资源不存在或类型不匹配时返回空。</returns>
        private static async Task<T> LoadResourceTaskAsync<T>(string path, CancellationToken token) where T : class
        {
            var asset = await LoadUnityObjectTaskAsync(path, typeof(T), token);
            return asset as T;
        }

        /// <summary>
        /// 等待 TextAsset 并在 Unity 主线程上下文中读取二进制内容。
        /// </summary>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <param name="token">本次等待使用的取消令牌。</param>
        /// <returns>资源二进制内容；资源不存在时返回空。</returns>
        private static async Task<byte[]> LoadRawBytesTaskAsync(string path, CancellationToken token)
        {
            var asset = await LoadTextAssetTaskAsync(path, token);
            return asset != default ? asset.bytes : null;
        }

        /// <summary>
        /// 等待 TextAsset 并在 Unity 主线程上下文中读取文本内容。
        /// </summary>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <param name="token">本次等待使用的取消令牌。</param>
        /// <returns>资源文本内容；资源不存在时返回空。</returns>
        private static async Task<string> LoadRawTextTaskAsync(string path, CancellationToken token)
        {
            var asset = await LoadTextAssetTaskAsync(path, token);
            return asset != default ? asset.text : null;
        }

        /// <summary>
        /// 复用统一 ResourceRequest 等待逻辑加载 TextAsset。
        /// </summary>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <param name="token">本次等待使用的取消令牌。</param>
        /// <returns>加载到的 TextAsset；资源不存在时返回空。</returns>
        private static async Task<TextAsset> LoadTextAssetTaskAsync(string path, CancellationToken token)
        {
            var asset = await LoadUnityObjectTaskAsync(path, typeof(TextAsset), token);
            return asset as TextAsset;
        }

        /// <summary>
        /// 在未安装 UniTask 时创建并等待一次 Unity ResourceRequest，提供纯 Task 回退。
        /// </summary>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <param name="requestedType">调用方期望的资源类型。</param>
        /// <param name="token">本次等待使用的取消令牌。</param>
        /// <returns>Unity 返回的资源对象；资源不存在时返回空。</returns>
        private static Task<Object> LoadUnityObjectTaskAsync(
            string path,
            Type requestedType,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var resourceType = ResolveResourceType(requestedType);
            var request = resourceType != null
                ? Resources.LoadAsync(path, resourceType)
                : Resources.LoadAsync(path);
            return new ResourceRequestCompletion(request, token).Task;
        }
#endif

        /// <summary>
        /// 按请求类型同步加载资源；非 Unity 类型回退为无类型加载以支持资源实现的接口。
        /// </summary>
        /// <param name="path">不含扩展名的 Resources 相对路径。</param>
        /// <param name="requestedType">调用方期望的资源类型。</param>
        /// <returns>Unity 返回的资源对象；资源不存在时返回空。</returns>
        private static Object LoadUnityObject(string path, Type requestedType)
        {
            var resourceType = ResolveResourceType(requestedType);
            return resourceType != null
                ? Resources.Load(path, resourceType)
                : Resources.Load(path);
        }

        /// <summary>
        /// 把框架请求类型转换为 Resources 可用于类型过滤的 UnityEngine.Object 类型。
        /// </summary>
        /// <param name="requestedType">调用方期望的资源类型。</param>
        /// <returns>可传给 Resources 的类型；接口等非 Unity 类型返回空并使用无类型加载。</returns>
        private static Type ResolveResourceType(Type requestedType)
        {
            return typeof(Object).IsAssignableFrom(requestedType) ? requestedType : null;
        }

#if !YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 协调 ResourceRequest 完成与取消竞争，并确保完成回调和注册最终解除。
        /// </summary>
        private sealed class ResourceRequestCompletion
        {
            private readonly object mLock = new();
            private readonly ResourceRequest mRequest;
            private readonly CancellationToken mToken;
            private readonly TaskCompletionSource<Object> mCompletion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private CancellationTokenRegistration mRegistration;
            private RequestState mState;
            private bool mHasRegistration;
            private bool mRequestFinished;

            /// <summary>
            /// 订阅一次请求完成事件，并在令牌可取消时注册独立等待取消。
            /// </summary>
            /// <param name="request">Unity 已创建的资源请求。</param>
            /// <param name="token">本次等待使用的取消令牌。</param>
            internal ResourceRequestCompletion(ResourceRequest request, CancellationToken token)
            {
                mRequest = request;
                mToken = token;
                mRequest.completed += OnRequestCompleted;
                RegisterCancellation(token);
            }

            /// <summary>
            /// 获取仅代表本次等待结果的 Task。
            /// </summary>
            internal Task<Object> Task => mCompletion.Task;

            /// <summary>
            /// 注册取消回调，并处理请求可能先于注册完成的竞争。
            /// </summary>
            /// <param name="token">本次等待使用的取消令牌。</param>
            private void RegisterCancellation(CancellationToken token)
            {
                if (!token.CanBeCanceled)
                {
                    return;
                }

                var registration = token.Register(OnCancelled);
                bool disposeImmediately;
                lock (mLock)
                {
                    mRegistration = registration;
                    mHasRegistration = true;
                    disposeImmediately = mRequestFinished;
                }

                if (disposeImmediately)
                {
                    registration.Dispose();
                }
            }

            /// <summary>
            /// 在 Unity 请求完成时提交资源结果；已经取消的等待不会重新变为成功。
            /// </summary>
            /// <param name="_">Unity 完成的异步操作，本类型已持有对应请求。</param>
            private void OnRequestCompleted(AsyncOperation _)
            {
                mRequest.completed -= OnRequestCompleted;
                bool completeWithResult;
                CancellationTokenRegistration registration = default;
                lock (mLock)
                {
                    mRequestFinished = true;
                    completeWithResult = mState == RequestState.Pending;
                    if (completeWithResult)
                    {
                        mState = RequestState.Completed;
                    }

                    if (mHasRegistration)
                    {
                        registration = mRegistration;
                    }
                }

                registration.Dispose();
                if (completeWithResult)
                {
                    mCompletion.TrySetResult(mRequest.asset);
                }
            }

            /// <summary>
            /// 结束当前等待但不尝试取消 Unity 不支持取消的底层 ResourceRequest。
            /// </summary>
            private void OnCancelled()
            {
                lock (mLock)
                {
                    if (mState != RequestState.Pending)
                    {
                        return;
                    }

                    mState = RequestState.Cancelled;
                }

                mCompletion.TrySetCanceled(mToken);
            }
        }

        /// <summary>
        /// 描述 ResourceRequest 等待的终态，防止完成与取消互相覆盖。
        /// </summary>
        private enum RequestState
        {
            Pending,
            Completed,
            Cancelled
        }
#endif
    }
}
#endif
