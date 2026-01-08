#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections.Generic;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 原始文件加载池
    /// </summary>
    public class YooAssetRawFileLoaderPool : IRawFileLoaderPool
    {
        private readonly Stack<IRawFileLoader> mPool = new();
        private readonly ResourcePackage mPackage;

        /// <summary>
        /// 使用默认包名创建加载池
        /// </summary>
        public YooAssetRawFileLoaderPool() : this(YooAssets.GetPackage("DefaultPackage"))
        {
        }

        /// <summary>
        /// 使用指定包名创建加载池
        /// </summary>
        public YooAssetRawFileLoaderPool(string packageName) : this(YooAssets.GetPackage(packageName))
        {
        }

        /// <summary>
        /// 使用指定资源包创建加载池
        /// </summary>
        public YooAssetRawFileLoaderPool(ResourcePackage package)
        {
            mPackage = package ?? throw new ArgumentNullException(nameof(package));
        }

        public IRawFileLoader Allocate()
        {
            return mPool.Count > 0 ? mPool.Pop() : new YooAssetRawFileLoader(this, mPackage);
        }

        public void Recycle(IRawFileLoader loader) => mPool.Push(loader);
    }
}
#endif
