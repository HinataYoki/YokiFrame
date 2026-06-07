#if YOKIFRAME_YOOASSET_SUPPORT
#if YOOASSET_3_0_OR_NEWER
using YooAsset;
#endif

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 原始文件加载池。
    /// </summary>
    public class YooAssetRawFileLoaderPool : AbstractRawFileLoaderPool
    {
        internal readonly IYooAssetRawFileProvider Provider;

#if YOOASSET_3_0_OR_NEWER
        public YooAssetRawFileLoaderPool()
        {
            Provider = new YooAssetV3RawFileProvider();
        }
#else
        public YooAssetRawFileLoaderPool()
        {
            Provider = new YooAssetV2RawFileProvider();
        }
#endif

        protected override IRawFileLoader CreateLoader()
            => new YooAssetRawFileLoader(this, Provider);
    }
}
#endif
