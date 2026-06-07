#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT

namespace YokiFrame
{
    /// <summary>
    /// YooAsset UniTask 原始文件加载池。
    /// 继承 YooAssetRawFileLoaderPool，仅覆写 Loader 创建为 UniTask 版本。
    /// </summary>
    public sealed class YooAssetRawFileLoaderUniTaskPool : YooAssetRawFileLoaderPool
    {
        public YooAssetRawFileLoaderUniTaskPool() : base() { }

        protected override IRawFileLoader CreateLoader()
        {
            var uniTaskProvider = Provider as IYooAssetRawFileUniTaskProvider;
            return new YooAssetRawFileLoaderUniTask(this, uniTaskProvider);
        }
    }
}
#endif
