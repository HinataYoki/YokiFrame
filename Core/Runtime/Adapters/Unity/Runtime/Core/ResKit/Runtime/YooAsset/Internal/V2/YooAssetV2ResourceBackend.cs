#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_2_3_OR_NEWER && !YOOASSET_3_0_OR_NEWER
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
    internal sealed class YooAssetV2ResourceBackend : IYooAssetResourceBackend
    {
        public AssetHandle LoadAsset(string path, Type assetType)
        {
            return YooAssets.LoadAssetSync(path, assetType);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<AssetHandle> LoadAssetAsync(string path, Type assetType, CancellationToken token)
#else
        public async Task<AssetHandle> LoadAssetAsync(string path, Type assetType, CancellationToken token)
#endif
        {
            var handle = YooAssets.LoadAssetAsync(path, assetType);
#if YOKIFRAME_UNITASK_SUPPORT
            await YooAssetHandleAwaiter.WaitAsync(handle, token);
#else
            await YooAssetHandleAwaiter.WaitAsync(handle, token).ConfigureAwait(false);
#endif
            return handle;
        }
    }
}
#endif
#endif
