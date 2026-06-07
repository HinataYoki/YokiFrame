#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 原始文件加载器 UniTask 扩展。
    /// 继承版本无关的 YooAssetRawFileLoader，仅追加 UniTask 异步方法。
    /// </summary>
    public sealed class YooAssetRawFileLoaderUniTask : YooAssetRawFileLoader, IRawFileLoaderUniTask
    {
        private readonly IYooAssetRawFileUniTaskProvider mUniTaskProvider;

        internal YooAssetRawFileLoaderUniTask(
            IRawFileLoaderPool pool, IYooAssetRawFileUniTaskProvider provider)
            : base(pool, provider)
        {
            mUniTaskProvider = provider;
        }

        public async UniTask<string> LoadRawFileTextUniTaskAsync(
            string path, CancellationToken cancellationToken = default)
        {
            return await mUniTaskProvider.LoadTextUniTaskAsync(path, cancellationToken);
        }

        public async UniTask<byte[]> LoadRawFileDataUniTaskAsync(
            string path, CancellationToken cancellationToken = default)
        {
            return await mUniTaskProvider.LoadDataUniTaskAsync(path, cancellationToken);
        }
    }
}
#endif
