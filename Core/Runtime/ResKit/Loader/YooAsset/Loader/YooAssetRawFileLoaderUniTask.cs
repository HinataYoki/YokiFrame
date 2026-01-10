#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 原始文件加载池（智能查找包，支持 UniTask）
    /// </summary>
    public class YooAssetRawFileLoaderUniTaskPool : IRawFileLoaderPool
    {
        private readonly Stack<IRawFileLoader> mPool = new();

        public IRawFileLoader Allocate() => mPool.Count > 0 ? mPool.Pop() : new YooAssetRawFileLoaderUniTask(this);
        public void Recycle(IRawFileLoader loader) => mPool.Push(loader);
    }

    /// <summary>
    /// YooAsset 原始文件加载器（智能查找包，支持 UniTask）
    /// </summary>
    public class YooAssetRawFileLoaderUniTask : IRawFileLoaderUniTask
    {
        private readonly IRawFileLoaderPool mPool;
        private RawFileHandle mHandle;

        public YooAssetRawFileLoaderUniTask(IRawFileLoaderPool pool) => mPool = pool;

        /// <summary>
        /// 智能查找包含路径的包
        /// </summary>
        private static ResourcePackage FindPackage(string path)
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

        public async UniTask<string> LoadRawFileTextUniTaskAsync(string path, CancellationToken cancellationToken = default)
        {
            var package = FindPackage(path);
            mHandle = package.LoadRawFileAsync(path);
            await mHandle.ToUniTask(cancellationToken: cancellationToken);
            return mHandle.GetRawFileText();
        }

        public async UniTask<byte[]> LoadRawFileDataUniTaskAsync(string path, CancellationToken cancellationToken = default)
        {
            var package = FindPackage(path);
            mHandle = package.LoadRawFileAsync(path);
            await mHandle.ToUniTask(cancellationToken: cancellationToken);
            return mHandle.GetRawFileData();
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
