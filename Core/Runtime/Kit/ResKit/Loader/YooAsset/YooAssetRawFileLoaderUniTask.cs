#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 原始文件加载池（支持 UniTask）
    /// </summary>
    public class YooAssetRawFileLoaderUniTaskPool : IRawFileLoaderPool
    {
        private readonly Stack<IRawFileLoader> mPool = new();
        private readonly ResourcePackage mPackage;

        public YooAssetRawFileLoaderUniTaskPool() : this(YooAssets.GetPackage("DefaultPackage")) { }
        public YooAssetRawFileLoaderUniTaskPool(string packageName) : this(YooAssets.GetPackage(packageName)) { }
        public YooAssetRawFileLoaderUniTaskPool(ResourcePackage package)
            => mPackage = package ?? throw new ArgumentNullException(nameof(package));

        public IRawFileLoader Allocate() => mPool.Count > 0 ? mPool.Pop() : new YooAssetRawFileLoaderUniTask(this, mPackage);
        public void Recycle(IRawFileLoader loader) => mPool.Push(loader);
    }

    /// <summary>
    /// YooAsset 原始文件加载器（支持 UniTask）
    /// </summary>
    public class YooAssetRawFileLoaderUniTask : IRawFileLoaderUniTask
    {
        private readonly IRawFileLoaderPool mPool;
        private readonly ResourcePackage mPackage;
        private RawFileHandle mHandle;

        public YooAssetRawFileLoaderUniTask(IRawFileLoaderPool pool, ResourcePackage package)
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

        public async UniTask<string> LoadRawFileTextUniTaskAsync(string path, CancellationToken cancellationToken = default)
        {
            mHandle = mPackage.LoadRawFileAsync(path);
            await mHandle.ToUniTask(cancellationToken: cancellationToken);
            return mHandle.GetRawFileText();
        }

        public async UniTask<byte[]> LoadRawFileDataUniTaskAsync(string path, CancellationToken cancellationToken = default)
        {
            mHandle = mPackage.LoadRawFileAsync(path);
            await mHandle.ToUniTask(cancellationToken: cancellationToken);
            return mHandle.GetRawFileData();
        }

        public void UnloadAndRecycle() { mHandle?.Release(); mHandle = null; mPool.Recycle(this); }
    }
}
#endif
