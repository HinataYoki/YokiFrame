#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 原始文件加载池（支持 UniTask） - 继承 YooAssetRawFileLoaderPool
    /// </summary>
    public class YooAssetRawFileLoaderUniTaskPool : YooAssetRawFileLoaderPool
    {
        protected override IRawFileLoader CreateLoader() => new YooAssetRawFileLoaderUniTask(this);
    }

    /// <summary>
    /// YooAsset 原始文件加载器（支持 UniTask） - 继承 YooAssetRawFileLoader，仅扩展 UniTask 异步方法
    /// </summary>
    public class YooAssetRawFileLoaderUniTask : YooAssetRawFileLoader, IRawFileLoaderUniTask
    {
        public YooAssetRawFileLoaderUniTask(IRawFileLoaderPool pool) : base(pool) { }

        public async UniTask<string> LoadRawFileTextUniTaskAsync(string path, CancellationToken cancellationToken = default)
        {
            var package = FindPackage(path);
            mHandle = package.LoadRawFileAsync(path);
            await mHandle.ToUniTask(cancellationToken: cancellationToken);
            return mHandle.GetRawFileText();
        }

        public async UniTask<byte[]> LoadRawFileDataUniTaskAsync(string path, CancellationToken cancellationToken = default)
        {
            var package = FindPackage(path);
            mHandle = package.LoadRawFileAsync(path);
            await mHandle.ToUniTask(cancellationToken: cancellationToken);
            return mHandle.GetRawFileData();
        }
    }
}
#endif
