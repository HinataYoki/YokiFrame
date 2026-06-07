#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_2_3_OR_NEWER
using System;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 2.x 原始文件加载池
    /// </summary>
    public class YooAssetRawFileLoaderPool : AbstractRawFileLoaderPool
    {
        protected override IRawFileLoader CreateLoader() => new YooAssetRawFileLoader(this);
    }

    /// <summary>
    /// YooAsset 2.x 原始文件加载器
    /// 2.x 使用 YooAssets 静态 API，无 ResourcePackage
    /// </summary>
    public class YooAssetRawFileLoader : IRawFileLoader
    {
        protected readonly IRawFileLoaderPool mPool;
        protected RawFileOperationHandle mHandle;

        public YooAssetRawFileLoader(IRawFileLoaderPool pool) => mPool = pool;

        public string LoadRawFileText(string path)
        {
            mHandle = YooAssets.LoadRawFileSync(path);
            return mHandle.GetRawFileText();
        }

        public byte[] LoadRawFileData(string path)
        {
            mHandle = YooAssets.LoadRawFileSync(path);
            return mHandle.GetRawFileData();
        }

        public string GetRawFilePath(string path)
        {
            mHandle = YooAssets.LoadRawFileSync(path);
            return mHandle.GetRawFilePath();
        }

        public void LoadRawFileTextAsync(string path, Action<string> onComplete)
        {
            mHandle = YooAssets.LoadRawFileAsync(path);
            mHandle.Completed += handle => onComplete?.Invoke(handle.GetRawFileText());
        }

        public void LoadRawFileDataAsync(string path, Action<byte[]> onComplete)
        {
            mHandle = YooAssets.LoadRawFileAsync(path);
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
