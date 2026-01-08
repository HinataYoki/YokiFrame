#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using YooAsset;

namespace YokiFrame
{
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

        public string LoadRawFileText(string path)
        {
            mHandle = mPackage.LoadRawFileSync(path);
            return mHandle.GetRawFileText();
        }

        public byte[] LoadRawFileData(string path)
        {
            mHandle = mPackage.LoadRawFileSync(path);
            return mHandle.GetRawFileData();
        }

        public void LoadRawFileTextAsync(string path, Action<string> onComplete)
        {
            mHandle = mPackage.LoadRawFileAsync(path);
            mHandle.Completed += handle =>
            {
                onComplete?.Invoke(handle.GetRawFileText());
            };
        }

        public void LoadRawFileDataAsync(string path, Action<byte[]> onComplete)
        {
            mHandle = mPackage.LoadRawFileAsync(path);
            mHandle.Completed += handle =>
            {
                onComplete?.Invoke(handle.GetRawFileData());
            };
        }

        public string GetRawFilePath(string path)
        {
            mHandle = mPackage.LoadRawFileSync(path);
            return mHandle.GetRawFilePath();
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
