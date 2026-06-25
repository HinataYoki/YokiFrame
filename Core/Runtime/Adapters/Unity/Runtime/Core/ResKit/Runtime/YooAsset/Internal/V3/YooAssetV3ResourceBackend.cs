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
    internal sealed class YooAssetV3ResourceBackend : IYooAssetResourceBackend
    {
        private const string DEFAULT_PACKAGE_NAME = "DefaultPackage";

        private readonly ResourcePackage mPackage;
        private readonly string mPackageName;

        public YooAssetV3ResourceBackend(ResourcePackage package)
        {
            mPackage = package ?? throw new ArgumentNullException(nameof(package));
        }

        public YooAssetV3ResourceBackend(string packageName)
        {
            mPackageName = string.IsNullOrEmpty(packageName) ? DEFAULT_PACKAGE_NAME : packageName;
        }

        public AssetHandle LoadAsset(string path, Type assetType)
        {
            return ResolvePackage().LoadAssetSync(path, assetType);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<AssetHandle> LoadAssetAsync(string path, Type assetType, CancellationToken token)
#else
        public async Task<AssetHandle> LoadAssetAsync(string path, Type assetType, CancellationToken token)
#endif
        {
            var handle = ResolvePackage().LoadAssetAsync(path, assetType);
#if YOKIFRAME_UNITASK_SUPPORT
            await YooAssetHandleAwaiter.WaitAsync(handle, token);
#else
            await YooAssetHandleAwaiter.WaitAsync(handle, token).ConfigureAwait(false);
#endif
            return handle;
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
