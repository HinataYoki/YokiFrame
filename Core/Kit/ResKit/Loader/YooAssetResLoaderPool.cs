#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections.Generic;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 资源加载池
    /// </summary>
    public class YooAssetResLoaderPool : IResLoaderPool
    {
        private readonly Stack<IResLoader> mPool = new();
        private readonly ResourcePackage mPackage;

        /// <summary>
        /// 使用默认包名创建加载池
        /// </summary>
        public YooAssetResLoaderPool() : this(YooAssets.GetPackage("DefaultPackage"))
        {
        }

        /// <summary>
        /// 使用指定包名创建加载池
        /// </summary>
        public YooAssetResLoaderPool(string packageName) : this(YooAssets.GetPackage(packageName))
        {
        }

        /// <summary>
        /// 使用指定资源包创建加载池
        /// </summary>
        public YooAssetResLoaderPool(ResourcePackage package)
        {
            mPackage = package ?? throw new ArgumentNullException(nameof(package));
        }

        public IResLoader Allocate()
        {
            return mPool.Count > 0 ? mPool.Pop() : new YooAssetResLoader(this, mPackage);
        }

        public void Recycle(IResLoader loader) => mPool.Push(loader);
    }
}
#endif
