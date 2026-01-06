#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Collections.Generic;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 资源加载池（支持 UniTask）
    /// </summary>
    public class YooAssetResLoaderUniTaskPool : IResLoaderPool
    {
        private readonly Stack<IResLoader> mPool = new();
        private readonly ResourcePackage mPackage;

        /// <summary>
        /// 使用默认包名创建加载池
        /// </summary>
        public YooAssetResLoaderUniTaskPool() : this(YooAssets.GetPackage("DefaultPackage"))
        {
        }

        /// <summary>
        /// 使用指定包名创建加载池
        /// </summary>
        public YooAssetResLoaderUniTaskPool(string packageName) : this(YooAssets.GetPackage(packageName))
        {
        }

        /// <summary>
        /// 使用指定资源包创建加载池
        /// </summary>
        public YooAssetResLoaderUniTaskPool(ResourcePackage package)
        {
            mPackage = package ?? throw new ArgumentNullException(nameof(package));
        }

        public IResLoader Allocate()
        {
            return mPool.Count > 0 ? mPool.Pop() : new YooAssetResLoaderUniTask(this, mPackage);
        }

        public void Recycle(IResLoader loader) => mPool.Push(loader);
    }
}
#endif
