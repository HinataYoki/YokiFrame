#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_2_3_OR_NEWER && YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 2.x 原始文件加载池（支持 UniTask）
    /// </summary>
    public class YooAssetRawFileLoaderUniTaskPool : YooAssetRawFileLoaderPool
    {
        protected override IRawFileLoader CreateLoader() => new YooAssetRawFileLoaderUniTask(this);
    }

    /// <summary>
    /// YooAsset 2.x 原始文件加载器（支持 UniTask）
    /// </summary>
    public class YooAssetRawFileLoaderUniTask : YooAssetRawFileLoader, IRawFileLoaderUniTask
    {
        public YooAssetRawFileLoaderUniTask(IRawFileLoaderPool pool) : base(pool) { }

        public async UniTask<string> LoadRawFileTextUniTaskAsync(string path, CancellationToken cancellationToken = default)
        {
            mHandle = YooAssets.LoadRawFileAsync(path);
            await mHandle.ToUniTask(cancellationToken: cancellationToken);
            return mHandle.GetRawFileText();
        }

        public async UniTask<byte[]> LoadRawFileDataUniTaskAsync(string path, CancellationToken cancellationToken = default)
        {
            mHandle = YooAssets.LoadRawFileAsync(path);
            await mHandle.ToUniTask(cancellationToken: cancellationToken);
            return mHandle.GetRawFileData();
        }
    }
}
#endif
