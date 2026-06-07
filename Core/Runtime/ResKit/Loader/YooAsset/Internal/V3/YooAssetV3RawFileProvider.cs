#if YOOASSET_3_0_OR_NEWER
using System;
using YooAsset;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 3.x 原始文件提供者。
    /// 使用 RawFileObject 作为资源类型，通过 ResourcePackage 加载。
    /// </summary>
    internal sealed class YooAssetV3RawFileProvider : IYooAssetRawFileProvider
#if YOKIFRAME_UNITASK_SUPPORT
        , IYooAssetRawFileUniTaskProvider
#endif
    {
        private AssetHandle mHandle;

        public string LoadText(string path)
        {
            var package = FindPackage(path);
            mHandle = package.LoadAssetSync<RawFileObject>(path);
            return mHandle.GetAssetObject<RawFileObject>().GetText();
        }

        public byte[] LoadData(string path)
        {
            var package = FindPackage(path);
            mHandle = package.LoadAssetSync<RawFileObject>(path);
            return mHandle.GetAssetObject<RawFileObject>().GetBytes();
        }

        public string GetFilePath(string path)
        {
            var package = FindPackage(path);
            mHandle = package.LoadAssetSync<RawFileObject>(path);
            return mHandle.GetAssetInfo().AssetPath;
        }

        public void LoadTextAsync(string path, Action<string> onComplete)
        {
            var package = FindPackage(path);
            mHandle = package.LoadAssetAsync<RawFileObject>(path);
            mHandle.Completed += h =>
            {
                var rawObj = h.GetAssetObject<RawFileObject>();
                onComplete?.Invoke(rawObj != default ? rawObj.GetText() : null);
            };
        }

        public void LoadDataAsync(string path, Action<byte[]> onComplete)
        {
            var package = FindPackage(path);
            mHandle = package.LoadAssetAsync<RawFileObject>(path);
            mHandle.Completed += h =>
            {
                var rawObj = h.GetAssetObject<RawFileObject>();
                onComplete?.Invoke(rawObj != default ? rawObj.GetBytes() : null);
            };
        }

        public void ReleaseHandle()
        {
            mHandle?.Release();
            mHandle = null;
        }

        private static ResourcePackage FindPackage(string path)
        {
            if (YooInit.Initialized)
                return YooInit.FindPackageForPath(path);
            return YooAssets.GetPackage("DefaultPackage");
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<string> LoadTextUniTaskAsync(string path, CancellationToken ct)
        {
            var package = FindPackage(path);
            mHandle = package.LoadAssetAsync<RawFileObject>(path);
            await mHandle.ToUniTask(cancellationToken: ct);
            var rawObj = mHandle.GetAssetObject<RawFileObject>();
            return rawObj != default ? rawObj.GetText() : null;
        }

        public async UniTask<byte[]> LoadDataUniTaskAsync(string path, CancellationToken ct)
        {
            var package = FindPackage(path);
            mHandle = package.LoadAssetAsync<RawFileObject>(path);
            await mHandle.ToUniTask(cancellationToken: ct);
            var rawObj = mHandle.GetAssetObject<RawFileObject>();
            return rawObj != default ? rawObj.GetBytes() : null;
        }
#endif
    }
}
#endif
