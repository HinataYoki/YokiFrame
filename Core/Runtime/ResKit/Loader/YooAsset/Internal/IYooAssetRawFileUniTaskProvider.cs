#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 原始文件加载 UniTask 能力扩展。
    /// </summary>
    internal interface IYooAssetRawFileUniTaskProvider : IYooAssetRawFileProvider
    {
        UniTask<string> LoadTextUniTaskAsync(string path, CancellationToken ct);
        UniTask<byte[]> LoadDataUniTaskAsync(string path, CancellationToken ct);
    }
}
#endif
