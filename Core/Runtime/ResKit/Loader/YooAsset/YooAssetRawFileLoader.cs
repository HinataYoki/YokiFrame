#if YOKIFRAME_YOOASSET_SUPPORT
using System;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 原始文件加载器。
    /// 版本无关 — V2/V3 差异通过 IYooAssetRawFileProvider 隔离。
    /// 本文件零内部 #if。
    /// </summary>
    public class YooAssetRawFileLoader : IRawFileLoader
    {
        private readonly IRawFileLoaderPool mPool;
        private readonly IYooAssetRawFileProvider mProvider;

        internal YooAssetRawFileLoader(IRawFileLoaderPool pool, IYooAssetRawFileProvider provider)
        {
            mPool = pool;
            mProvider = provider;
        }

        public string LoadRawFileText(string path)
            => mProvider.LoadText(path);

        public byte[] LoadRawFileData(string path)
            => mProvider.LoadData(path);

        public string GetRawFilePath(string path)
            => mProvider.GetFilePath(path);

        public void LoadRawFileTextAsync(string path, Action<string> onComplete)
            => mProvider.LoadTextAsync(path, onComplete);

        public void LoadRawFileDataAsync(string path, Action<byte[]> onComplete)
            => mProvider.LoadDataAsync(path, onComplete);

        public void UnloadAndRecycle()
        {
            mProvider.ReleaseHandle();
            mPool.Recycle(this);
        }
    }
}
#endif
