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

        public YooAssetRawFileLoaderPool() : this(YooAssets.GetPackage("DefaultPackage")) { }
        public YooAssetRawFileLoaderPool(string packageName) : this(YooAssets.GetPackage(packageName)) { }
        public YooAssetRawFileLoaderPool(ResourcePackage package)
            => mPackage = package ?? throw new ArgumentNullException(nameof(package));

        public IRawFileLoader Allocate() => mPool.Count > 0 ? mPool.Pop() : new YooAssetRawFileLoader(this, mPackage);
        public void Recycle(IRawFileLoader loader) => mPool.Push(loader);
    }

    /// <summary>
    /// YooAsset 原始文件加载器
    /// </summary>
    public class YooAssetRawFileLoader : IRawFileLoader
    {
        private readonly IRawFileLoaderPool mPool;
        private readonly ResourcePackage mPackage;
        private RawFileHandle mHandle;

        public YooAssetRawFileLoader(IRawFileLoaderPool pool, ResourcePackage package)
        {
            mPool = pool;
            mPackage = package;
        }

        public string LoadRawFileText(string path) { mHandle = mPackage.LoadRawFileSync(path); return mHandle.GetRawFileText(); }
        public byte[] LoadRawFileData(string path) { mHandle = mPackage.LoadRawFileSync(path); return mHandle.GetRawFileData(); }
        public string GetRawFilePath(string path) { mHandle = mPackage.LoadRawFileSync(path); return mHandle.GetRawFilePath(); }

        public void LoadRawFileTextAsync(string path, Action<string> onComplete)
        {
            mHandle = mPackage.LoadRawFileAsync(path);
            mHandle.Completed += handle => onComplete?.Invoke(handle.GetRawFileText());
        }

        public void LoadRawFileDataAsync(string path, Action<byte[]> onComplete)
        {
            mHandle = mPackage.LoadRawFileAsync(path);
            mHandle.Completed += handle => onComplete?.Invoke(handle.GetRawFileData());
        }

        public void UnloadAndRecycle() { mHandle?.Release(); mHandle = null; mPool.Recycle(this); }
    }
}
#endif
