#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
#if YOOASSET_3_0_OR_NEWER
using YooAsset;
#endif

namespace YokiFrame
{
    /// <summary>
    /// YooAsset UniTask 资源加载池。
    /// 继承 YooAssetResLoaderPool，仅覆写 Loader 创建为 UniTask 版本。
    /// </summary>
    public class YooAssetResLoaderUniTaskPool : YooAssetResLoaderPool
    {
        public YooAssetResLoaderUniTaskPool() : base() { }
#if YOOASSET_3_0_OR_NEWER
        public YooAssetResLoaderUniTaskPool(ResourcePackage package) : base(package) { }
#endif
        public YooAssetResLoaderUniTaskPool(string packageName) : base(packageName) { }

        protected override IResLoader CreateLoader()
        {
            var uniTaskProvider = Provider as IYooAssetResUniTaskProvider;
            return new YooAssetResLoaderUniTask(this, uniTaskProvider);
        }
    }
}
#endif
