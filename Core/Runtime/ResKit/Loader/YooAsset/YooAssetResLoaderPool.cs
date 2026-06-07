#if YOKIFRAME_YOOASSET_SUPPORT
using System;
#if YOOASSET_3_0_OR_NEWER
using YooAsset;
#endif

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 资源加载池。
    /// 构造时按 YooAsset 版本选择适配器 — 池的其余部分版本无关。
    /// </summary>
    public class YooAssetResLoaderPool : AbstractResLoaderPool
    {
        internal readonly IYooAssetResProvider Provider;

        public YooAssetResLoaderPool()
        {
#if YOOASSET_3_0_OR_NEWER
            Provider = new YooAssetV3ResProvider(YooAssets.GetPackage("DefaultPackage"));
#else
            Provider = new YooAssetV2ResProvider();
#endif
        }

#if YOOASSET_3_0_OR_NEWER
        public YooAssetResLoaderPool(ResourcePackage package)
        {
            Provider = new YooAssetV3ResProvider(package ?? throw new ArgumentNullException(nameof(package)));
        }

        public YooAssetResLoaderPool(string packageName)
            : this(YooAssets.GetPackage(packageName)) { }
#else
        public YooAssetResLoaderPool(string packageName) { Provider = new YooAssetV2ResProvider(); }
#endif

        protected override IResLoader CreateLoader()
            => new YooAssetResLoader(this, Provider);
    }
}
#endif
