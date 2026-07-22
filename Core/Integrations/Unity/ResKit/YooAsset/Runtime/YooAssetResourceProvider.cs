#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3

using System;
using System.Collections.Generic;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;
using YooSceneHandle = YooAsset.SceneHandle;

namespace YokiFrame.Unity
{
    /// <summary>把已经初始化成功的 YooAsset 2.3+ 或 3.x ResourcePackage 接入 ResKit。</summary>
    public sealed partial class YooAssetResourceProvider :
        IResourceProvider,
        IRawResourceProvider,
        IResSceneProvider,
        IResourceProviderCapabilities
    {
        private readonly object mLock = new();
        private readonly ResourcePackage mPackage;
        private readonly Dictionary<object, Stack<AssetHandle>> mHandles =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<string, YooSceneHandle> mSceneHandles =
            new(StringComparer.Ordinal);
        private ResSceneHandle mActiveScene;

        /// <summary>
        /// 创建绑定到指定资源包的 Provider；资源包初始化和销毁仍由项目负责。
        /// </summary>
        /// <param name="package">已经完成初始化并加载有效 manifest 的 YooAsset 资源包。</param>
        public YooAssetResourceProvider(ResourcePackage package)
        {
            mPackage = package ?? throw new ArgumentNullException(nameof(package));
            EnsurePackageReady();
#if UNITY_EDITOR
            ProviderName = "YooAsset:" + package.PackageName;
#endif
        }

#if UNITY_EDITOR
        /// <summary>获取包含资源包名的稳定 Provider 名称。</summary>
        public string ProviderName { get; }
#endif

        /// <summary>获取 YooAsset 是否支持读取 raw bytes。</summary>
        public bool SupportsRawBytes => true;

        /// <summary>获取 YooAsset 是否支持读取 raw 文本。</summary>
        public bool SupportsRawText => true;

        /// <summary>获取当前 YooAsset 场景后端名称。</summary>
        public string SceneBackendName => "YooAsset.Scene";

        /// <summary>获取当前由 Provider 记录的激活场景。</summary>
        public ResSceneHandle ActiveScene => mActiveScene;

        /// <summary>同步加载一个 UnityEngine.Object，并保存对应 YooAsset handle 的释放所有权。</summary>
        public T Load<T>(string path) where T : class
        {
            EnsureLoadRequest<T>(path);
            AssetHandle handle = mPackage.LoadAssetSync(path, typeof(T));
            try
            {
                return CompleteAssetLoad<T>(path, handle);
            }
            catch
            {
                ReleaseHandle(handle);
                throw;
            }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>异步加载一个 UnityEngine.Object；取消时立即释放本次 YooAsset handle。</summary>
        public async UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#else
        /// <summary>异步加载一个 UnityEngine.Object；取消时立即释放本次 YooAsset handle。</summary>
        public async Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#endif
        {
            EnsureLoadRequest<T>(path);
            AssetHandle handle = mPackage.LoadAssetAsync(path, typeof(T));
            try
            {
                await WaitForCompletion(handle, token);
                return CompleteAssetLoad<T>(path, handle);
            }
            catch
            {
                ReleaseHandle(handle);
                throw;
            }
        }

        /// <summary>释放与资源对象关联的一次 YooAsset handle；未知对象不会转发给 YooAsset。</summary>
        public void Release(object asset)
        {
            AssetHandle handle = TakeHandle(asset);
            ReleaseHandle(handle);
        }

#if YOKIFRAME_YOOASSET_3
        /// <summary>同步读取 raw bytes，并在复制数据后立即释放 YooAsset 3 临时 handle。</summary>
        public byte[] LoadRaw(string path)
        {
            return UseRawFile(path, static rawFile => rawFile.GetBytes());
        }

        /// <summary>同步读取 raw 文本，并在读取后立即释放 YooAsset 3 临时 handle。</summary>
        public string LoadRawText(string path)
        {
            return UseRawFile(path, static rawFile => rawFile.GetText());
        }
#else
        /// <summary>同步读取 raw bytes，并在复制数据后立即释放 YooAsset 2.3 临时 handle。</summary>
        public byte[] LoadRaw(string path)
        {
            return UseRawFile(path, static handle => handle.GetRawFileData());
        }

        /// <summary>同步读取 raw 文本，并在读取后立即释放 YooAsset 2.3 临时 handle。</summary>
        public string LoadRawText(string path)
        {
            return UseRawFile(path, static handle => handle.GetRawFileText());
        }
#endif

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>异步读取 raw bytes，并在复制数据后释放临时 YooAsset handle。</summary>
        public async UniTask<byte[]> LoadRawAsync(string path, CancellationToken token = default)
#else
        /// <summary>异步读取 raw bytes，并在复制数据后释放临时 YooAsset handle。</summary>
        public async Task<byte[]> LoadRawAsync(string path, CancellationToken token = default)
#endif
        {
#if YOKIFRAME_YOOASSET_3
            return await UseRawFileAsync(path, static rawFile => rawFile.GetBytes(), token);
#else
            return await UseRawFileAsync(path, static handle => handle.GetRawFileData(), token);
#endif
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>异步读取 raw 文本，并在读取后释放临时 YooAsset handle。</summary>
        public async UniTask<string> LoadRawTextAsync(string path, CancellationToken token = default)
#else
        /// <summary>异步读取 raw 文本，并在读取后释放临时 YooAsset handle。</summary>
        public async Task<string> LoadRawTextAsync(string path, CancellationToken token = default)
#endif
        {
#if YOKIFRAME_YOOASSET_3
            return await UseRawFileAsync(path, static rawFile => rawFile.GetText(), token);
#else
            return await UseRawFileAsync(path, static handle => handle.GetRawFileText(), token);
#endif
        }

        /// <summary>校验 YooAsset 全局状态和当前资源包 manifest 均已可用。</summary>
        private void EnsurePackageReady()
        {
#if YOKIFRAME_YOOASSET_3
            bool initialized = YooAssets.IsInitialized;
            bool succeeded = mPackage.InitializeStatus == EOperationStatus.Succeeded;
#else
            bool initialized = YooAssets.Initialized;
            bool succeeded = mPackage.InitializeStatus == EOperationStatus.Succeed;
#endif
            if (!initialized || !succeeded || !mPackage.PackageValid)
            {
                throw new InvalidOperationException(
                    "YooAsset ResourcePackage must be initialized successfully before installing the ResKit provider.");
            }
        }

        /// <summary>校验路径和类型，避免把无效请求交给 YooAsset。</summary>
        private void EnsureLoadRequest<T>(string path) where T : class
        {
            EnsureRequestPath(path);
            if (!typeof(UnityEngine.Object).IsAssignableFrom(typeof(T)))
            {
                throw new NotSupportedException("YooAsset provider only loads UnityEngine.Object resource types.");
            }
        }

        /// <summary>校验资源包状态和 location，确保同步与异步入口使用相同前置条件。</summary>
        private void EnsureRequestPath(string path)
        {
            EnsurePackageReady();
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Resource path cannot be empty.", nameof(path));
            }
        }

        /// <summary>验证 handle 状态、取得资源并登记后续释放所需的原生 handle。</summary>
        private T CompleteAssetLoad<T>(string path, AssetHandle handle) where T : class
        {
            EnsureHandleSucceeded(path, handle, false);
            var asset = handle.AssetObject as T;
            if (asset == null)
            {
                throw new InvalidOperationException("YooAsset returned an incompatible asset for '" + path + "'.");
            }

            RegisterHandle(asset, handle);
            return asset;
        }

        /// <summary>登记资源对象与 handle；不同 location 返回同一对象时保存独立 handle 栈。</summary>
        private void RegisterHandle(object asset, AssetHandle handle)
        {
            lock (mLock)
            {
                if (!mHandles.TryGetValue(asset, out var handles))
                {
                    handles = new Stack<AssetHandle>();
                    mHandles.Add(asset, handles);
                }

                handles.Push(handle);
            }
        }

        /// <summary>原子取出资源对象的一次 handle 所有权，实际释放在锁外执行。</summary>
        private AssetHandle TakeHandle(object asset)
        {
            if (ReferenceEquals(asset, null)) return null;
            lock (mLock)
            {
                if (!mHandles.TryGetValue(asset, out var handles) || handles.Count == 0) return null;
                AssetHandle handle = handles.Pop();
                if (handles.Count == 0) mHandles.Remove(asset);
                return handle;
            }
        }

        /// <summary>释放仍有效的 YooAsset handle；用于成功卸载和全部失败路径。</summary>
        private static void ReleaseHandle(HandleBase handle)
        {
            if (handle != null && handle.IsValid) handle.Release();
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>逐帧等待 YooAsset handle 完成，并让取消令牌及时终止本次调用。</summary>
        private static async UniTask WaitForCompletion(HandleBase handle, CancellationToken token)
#else
        /// <summary>逐帧等待 YooAsset handle 完成，并让取消令牌及时终止本次调用。</summary>
        private static async Task WaitForCompletion(HandleBase handle, CancellationToken token)
#endif
        {
            while (!handle.IsDone)
            {
                token.ThrowIfCancellationRequested();
#if YOKIFRAME_UNITASK_SUPPORT
                await UniTask.Yield(PlayerLoopTiming.Update, token);
#else
                await Task.Yield();
#endif
            }

            token.ThrowIfCancellationRequested();
        }

#if YOKIFRAME_YOOASSET_3
        /// <summary>同步读取 YooAsset 3 RawFileObject，并保证临时 handle 在所有结果路径释放。</summary>
        private TResult UseRawFile<TResult>(string path, Func<RawFileObject, TResult> selector)
        {
            EnsureRequestPath(path);
            AssetHandle handle = mPackage.LoadAssetSync<RawFileObject>(path);
            try
            {
                EnsureHandleSucceeded(path, handle, true);
                RawFileObject rawFile = handle.GetAssetObject<RawFileObject>();
                if (rawFile == null)
                {
                    throw new InvalidOperationException("YooAsset returned no raw file for '" + path + "'.");
                }

                return selector(rawFile);
            }
            finally
            {
                ReleaseHandle(handle);
            }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>异步读取 YooAsset 3 RawFileObject，并保证临时 handle 在取消和失败路径释放。</summary>
        private async UniTask<TResult> UseRawFileAsync<TResult>(
#else
        /// <summary>异步读取 YooAsset 3 RawFileObject，并保证临时 handle 在取消和失败路径释放。</summary>
        private async Task<TResult> UseRawFileAsync<TResult>(
#endif
            string path,
            Func<RawFileObject, TResult> selector,
            CancellationToken token)
        {
            EnsureRequestPath(path);
            AssetHandle handle = mPackage.LoadAssetAsync<RawFileObject>(path);
            try
            {
                await WaitForCompletion(handle, token);
                EnsureHandleSucceeded(path, handle, true);
                RawFileObject rawFile = handle.GetAssetObject<RawFileObject>();
                if (rawFile == null)
                {
                    throw new InvalidOperationException("YooAsset returned no raw file for '" + path + "'.");
                }

                return selector(rawFile);
            }
            finally
            {
                ReleaseHandle(handle);
            }
        }
#else
        /// <summary>同步读取 YooAsset 2.3 raw 文件，并保证临时 handle 在所有结果路径释放。</summary>
        private TResult UseRawFile<TResult>(string path, Func<RawFileHandle, TResult> selector)
        {
            EnsureRequestPath(path);
            RawFileHandle handle = mPackage.LoadRawFileSync(path);
            try
            {
                EnsureHandleSucceeded(path, handle, true);
                return selector(handle);
            }
            finally
            {
                ReleaseHandle(handle);
            }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>异步读取 YooAsset 2.3 raw 文件，并保证临时 handle 在取消和失败路径释放。</summary>
        private async UniTask<TResult> UseRawFileAsync<TResult>(
#else
        /// <summary>异步读取 YooAsset 2.3 raw 文件，并保证临时 handle 在取消和失败路径释放。</summary>
        private async Task<TResult> UseRawFileAsync<TResult>(
#endif
            string path,
            Func<RawFileHandle, TResult> selector,
            CancellationToken token)
        {
            EnsureRequestPath(path);
            RawFileHandle handle = mPackage.LoadRawFileAsync(path);
            try
            {
                await WaitForCompletion(handle, token);
                EnsureHandleSucceeded(path, handle, true);
                return selector(handle);
            }
            finally
            {
                ReleaseHandle(handle);
            }
        }
#endif

        /// <summary>验证 YooAsset handle 状态，并按版本读取对应错误字段。</summary>
        private static void EnsureHandleSucceeded(string path, HandleBase handle, bool rawFile)
        {
#if YOKIFRAME_YOOASSET_3
            bool succeeded = handle.Status == EOperationStatus.Succeeded;
            string error = handle.Error;
#else
            bool succeeded = handle.Status == EOperationStatus.Succeed;
            string error = handle.LastError;
#endif
            if (succeeded) return;
            string resourceKind = rawFile ? "raw file" : "asset";
            throw new InvalidOperationException(
                "YooAsset failed to load " + resourceKind + " '" + path + "': " + error);
        }

        /// <summary>按对象引用而非 Unity 重载相等规则管理 YooAsset handle。</summary>
        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance = new();

            /// <summary>判断两个 key 是否为同一托管引用。</summary>
            public new bool Equals(object left, object right) => ReferenceEquals(left, right);

            /// <summary>返回不受 Unity 对象重载影响的引用哈希。</summary>
            public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
    }
}

#endif
