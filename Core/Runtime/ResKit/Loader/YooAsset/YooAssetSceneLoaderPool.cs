#if YOKIFRAME_YOOASSET_SUPPORT
#if YOOASSET_3_0_OR_NEWER
using System;
using YooAsset;
#endif

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 场景加载池。
    /// </summary>
    public class YooAssetSceneLoaderPool : AbstractSceneResLoaderPool
    {
        internal readonly IYooAssetSceneProvider Provider;

#if YOOASSET_3_0_OR_NEWER
        public YooAssetSceneLoaderPool(ResourcePackage package)
        {
            Provider = new YooAssetV3SceneProvider(
                package ?? throw new ArgumentNullException(nameof(package)));
        }

        public YooAssetSceneLoaderPool(string packageName)
            : this(YooAssets.GetPackage(packageName)) { }

        public YooAssetSceneLoaderPool()
            : this(YooAssets.GetPackage("DefaultPackage")) { }
#else
        public YooAssetSceneLoaderPool()
        {
            Provider = new YooAssetV2SceneProvider();
        }
#endif

        protected override ISceneResLoader CreateLoader()
            => new YooAssetSceneLoader(this, Provider);
    }
}
#endif
