#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_3_0_OR_NEWER
using System;
using System.Threading;
using YooAsset;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame.Unity
{
    internal sealed class YooAssetV3RawFileBackend : IYooAssetRawFileBackend
    {
        private const string DEFAULT_PACKAGE_NAME = "DefaultPackage";

        private readonly ResourcePackage mPackage;
        private readonly string mPackageName;

        public YooAssetV3RawFileBackend(ResourcePackage package)
        {
            mPackage = package ?? throw new ArgumentNullException(nameof(package));
        }

        public YooAssetV3RawFileBackend(string packageName)
        {
            mPackageName = string.IsNullOrEmpty(packageName) ? DEFAULT_PACKAGE_NAME : packageName;
        }

        public byte[] LoadRaw(string path)
        {
            var handle = ResolvePackage().LoadAssetSync<RawFileObject>(path);
            try
            {
                var rawObject = handle.GetAssetObject<RawFileObject>();
                return rawObject != default ? rawObject.GetBytes() : null;
            }
            finally
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
            }
        }

        public string LoadRawText(string path)
        {
            var handle = ResolvePackage().LoadAssetSync<RawFileObject>(path);
            try
            {
                var rawObject = handle.GetAssetObject<RawFileObject>();
                return rawObject != default ? rawObject.GetText() : null;
            }
            finally
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
            }
        }

        public string GetRawFilePath(string path)
        {
            var handle = ResolvePackage().LoadAssetSync<RawFileObject>(path);
            try
            {
                return handle.GetAssetInfo().AssetPath;
            }
            finally
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
            }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<byte[]> LoadRawAsync(string path, CancellationToken token)
#else
        public async Task<byte[]> LoadRawAsync(string path, CancellationToken token)
#endif
        {
            var handle = ResolvePackage().LoadAssetAsync<RawFileObject>(path);
            try
            {
#if YOKIFRAME_UNITASK_SUPPORT
                await YooAssetHandleAwaiter.WaitAsync(handle, token);
#else
                await YooAssetHandleAwaiter.WaitAsync(handle, token).ConfigureAwait(false);
#endif
                var rawObject = handle.GetAssetObject<RawFileObject>();
                return rawObject != default ? rawObject.GetBytes() : null;
            }
            finally
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
            }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<string> LoadRawTextAsync(string path, CancellationToken token)
#else
        public async Task<string> LoadRawTextAsync(string path, CancellationToken token)
#endif
        {
            var handle = ResolvePackage().LoadAssetAsync<RawFileObject>(path);
            try
            {
#if YOKIFRAME_UNITASK_SUPPORT
                await YooAssetHandleAwaiter.WaitAsync(handle, token);
#else
                await YooAssetHandleAwaiter.WaitAsync(handle, token).ConfigureAwait(false);
#endif
                var rawObject = handle.GetAssetObject<RawFileObject>();
                return rawObject != default ? rawObject.GetText() : null;
            }
            finally
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
            }
        }

        private ResourcePackage ResolvePackage()
        {
            if (mPackage != null)
                return mPackage;

            return YooAssets.GetPackage(mPackageName);
        }
    }
}
#endif
#endif
