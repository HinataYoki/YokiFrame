#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_3_0_OR_NEWER
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
    /// 3.x 使用 LoadAssetSync&lt;RawFileObject&gt; 加载原始文件
    /// </summary>
    public class YooAssetRawFileLoader : IRawFileLoader
    {
        protected readonly IRawFileLoaderPool mPool;
        protected AssetHandle mHandle;

        public YooAssetRawFileLoader(IRawFileLoaderPool pool) => mPool = pool;

        /// <summary>
        /// 智能查找包含路径的包
        /// </summary>
        protected static ResourcePackage FindPackage(string path)
        {
            if (YooInit.Initialized)
                return YooInit.FindPackageForPath(path);

            return YooAssets.GetPackage("DefaultPackage");
        }

        public string LoadRawFileText(string path)
        {
            var package = FindPackage(path);
            mHandle = package.LoadAssetSync<RawFileObject>(path);
            return mHandle.GetAssetObject<RawFileObject>().GetText();
        }

        public byte[] LoadRawFileData(string path)
        {
            var package = FindPackage(path);
            mHandle = package.LoadAssetSync<RawFileObject>(path);
            return mHandle.GetAssetObject<RawFileObject>().GetBytes();
        }

        public string GetRawFilePath(string path)
        {
            var package = FindPackage(path);
            mHandle = package.LoadAssetSync<RawFileObject>(path);
            return mHandle.GetAssetInfo().AssetPath;
        }

        public void LoadRawFileTextAsync(string path, Action<string> onComplete)
        {
            var package = FindPackage(path);
            mHandle = package.LoadAssetAsync<RawFileObject>(path);
            mHandle.Completed += handle =>
            {
                var rawObj = handle.GetAssetObject<RawFileObject>();
                onComplete?.Invoke(rawObj != default ? rawObj.GetText() : null);
            };
        }

        public void LoadRawFileDataAsync(string path, Action<byte[]> onComplete)
        {
            var package = FindPackage(path);
            mHandle = package.LoadAssetAsync<RawFileObject>(path);
            mHandle.Completed += handle =>
            {
                var rawObj = handle.GetAssetObject<RawFileObject>();
                onComplete?.Invoke(rawObj != default ? rawObj.GetBytes() : null);
            };
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
