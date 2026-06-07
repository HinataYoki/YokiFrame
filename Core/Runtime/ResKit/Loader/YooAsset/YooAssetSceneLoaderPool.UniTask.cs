#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
#if YOOASSET_3_0_OR_NEWER
using YooAsset;
#endif

namespace YokiFrame
{
    /// <summary>
    /// YooAsset UniTask 场景加载池。
    /// 继承 YooAssetSceneLoaderPool，仅覆写 Loader 创建为 UniTask 版本。
    /// </summary>
    public sealed class YooAssetSceneLoaderUniTaskPool : YooAssetSceneLoaderPool
    {
#if YOOASSET_3_0_OR_NEWER
        public YooAssetSceneLoaderUniTaskPool(ResourcePackage package) : base(package) { }
        public YooAssetSceneLoaderUniTaskPool(string packageName) : base(packageName) { }
        public YooAssetSceneLoaderUniTaskPool() : base() { }
#else
        public YooAssetSceneLoaderUniTaskPool() : base() { }
#endif

        protected override ISceneResLoader CreateLoader()
            => new YooAssetSceneLoaderUniTask(this, Provider);
    }
}
#endif
