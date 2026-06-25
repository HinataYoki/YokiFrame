#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
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
    internal interface IYooAssetResourceBackend
    {
        AssetHandle LoadAsset(string path, Type assetType);
#if YOKIFRAME_UNITASK_SUPPORT
        UniTask<AssetHandle> LoadAssetAsync(string path, Type assetType, CancellationToken token);
#else
        Task<AssetHandle> LoadAssetAsync(string path, Type assetType, CancellationToken token);
#endif
    }
}
#endif
#endif
