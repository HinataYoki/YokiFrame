#if YOOASSET_2_3_OR_NEWER && !YOOASSET_3_0_OR_NEWER
using System;
using YooAsset;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 2.x 原始文件提供者。
    /// 使用 YooAssets 静态 API 和专用 RawFileHandle。
    /// </summary>
    internal sealed class YooAssetV2RawFileProvider : IYooAssetRawFileProvider
#if YOKIFRAME_UNITASK_SUPPORT
        , IYooAssetRawFileUniTaskProvider
#endif
    {
        private RawFileHandle mHandle;

        public string LoadText(string path)
        {
            mHandle = YooAssets.LoadRawFileSync(path);
            return mHandle.GetRawFileText();
        }

        public byte[] LoadData(string path)
        {
            mHandle = YooAssets.LoadRawFileSync(path);
            return mHandle.GetRawFileData();
        }

        public string GetFilePath(string path)
        {
            mHandle = YooAssets.LoadRawFileSync(path);
            return mHandle.GetRawFilePath();
        }

        public void LoadTextAsync(string path, Action<string> onComplete)
        {
            mHandle = YooAssets.LoadRawFileAsync(path);
            mHandle.Completed += h => onComplete?.Invoke(h.GetRawFileText());
        }

        public void LoadDataAsync(string path, Action<byte[]> onComplete)
        {
            mHandle = YooAssets.LoadRawFileAsync(path);
            mHandle.Completed += h => onComplete?.Invoke(h.GetRawFileData());
        }

        public void ReleaseHandle()
        {
            mHandle?.Release();
            mHandle = default;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<string> LoadTextUniTaskAsync(string path, CancellationToken ct)
        {
            mHandle = YooAssets.LoadRawFileAsync(path);
            await mHandle.ToUniTask(cancellationToken: ct);
            return mHandle.GetRawFileText();
        }

        public async UniTask<byte[]> LoadDataUniTaskAsync(string path, CancellationToken ct)
        {
            mHandle = YooAssets.LoadRawFileAsync(path);
            await mHandle.ToUniTask(cancellationToken: ct);
            return mHandle.GetRawFileData();
        }
#endif
    }
}
#endif
