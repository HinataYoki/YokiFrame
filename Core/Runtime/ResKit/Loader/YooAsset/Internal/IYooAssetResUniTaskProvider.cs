#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 资源加载 UniTask 能力扩展。
    /// V2/V3 Provider 各自实现，使 UniTask Loader 保持版本无关。
    /// </summary>
    internal interface IYooAssetResUniTaskProvider : IYooAssetResProvider
    {
        UniTask<T> LoadAssetUniTaskAsync<T>(string path, CancellationToken ct) where T : Object;
        UniTask<T[]> LoadAllAssetsUniTaskAsync<T>(string path, CancellationToken ct) where T : Object;
        UniTask<SubAssetsResult<T>> LoadSubAssetsUniTaskAsync<T>(string path, CancellationToken ct) where T : Object;
    }
}
#endif
