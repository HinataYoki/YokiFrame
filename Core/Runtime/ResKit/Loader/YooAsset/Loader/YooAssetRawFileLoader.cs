#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 原始文件加载池（智能查找包）
    /// </summary>
    public class YooAssetRawFileLoaderPool : AbstractRawFileLoaderPool
    {
        protected override IRawFileLoader CreateLoader() => new YooAssetRawFileLoader(this);
    }

    /// <summary>
    /// YooAsset 原始文件加载器（智能查找包）
    /// </summary>
    public class YooAssetRawFileLoader : IRawFileLoader
    {
        protected readonly IRawFileLoaderPool mPool;
        protected RawFileHandle mHandle;

        public YooAssetRawFileLoader(IRawFileLoaderPool pool) => mPool = pool;

        /// <summary>
        /// 智能查找包含路径的包
        /// </summary>
        protected static ResourcePackage FindPackage(string path)
        {
            // 优先使用 YooInit 的智能查找
            if (YooInit.Initialized)
                return YooInit.FindPackageForPath(path);

            // 回退到默认包
            return YooAssets.GetPackage("DefaultPackage");
        }

        public string LoadRawFileText(string path)
        {
            var package = FindPackage(path);
            mHandle = package.LoadRawFileSync(path);
            return mHandle.GetRawFileText();
        }

        public byte[] LoadRawFileData(string path)
        {
            var package = FindPackage(path);
            mHandle = package.LoadRawFileSync(path);
            return mHandle.GetRawFileData();
        }

        public string GetRawFilePath(string path)
        {
            var package = FindPackage(path);
            mHandle = package.LoadRawFileSync(path);
            return mHandle.GetRawFilePath();
        }

        public void LoadRawFileTextAsync(string path, Action<string> onComplete)
        {
            var package = FindPackage(path);
            mHandle = package.LoadRawFileAsync(path);
            mHandle.Completed += handle => onComplete?.Invoke(handle.GetRawFileText());
        }

        public void LoadRawFileDataAsync(string path, Action<byte[]> onComplete)
        {
            var package = FindPackage(path);
            mHandle = package.LoadRawFileAsync(path);
            mHandle.Completed += handle => onComplete?.Invoke(handle.GetRawFileData());
        }

        public void UnloadAndRecycle()
        {
            mHandle?.Release();
            mHandle = null;
            mPool.Recycle(this);
        }
    }
}
#endif
