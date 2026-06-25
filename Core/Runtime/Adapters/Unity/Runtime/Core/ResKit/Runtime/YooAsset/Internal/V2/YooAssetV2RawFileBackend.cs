#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_2_3_OR_NEWER && !YOOASSET_3_0_OR_NEWER
using System.Threading;
using YooAsset;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame.Unity
{
    internal sealed class YooAssetV2RawFileBackend : IYooAssetRawFileBackend
    {
        public byte[] LoadRaw(string path)
        {
            var handle = YooAssets.LoadRawFileSync(path);
            try
            {
                return YooAssetHandleAwaiter.IsSucceed(handle) ? handle.GetRawFileData() : null;
            }
            finally
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
            }
        }

        public string LoadRawText(string path)
        {
            var handle = YooAssets.LoadRawFileSync(path);
            try
            {
                return YooAssetHandleAwaiter.IsSucceed(handle) ? handle.GetRawFileText() : null;
            }
            finally
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
            }
        }

        public string GetRawFilePath(string path)
        {
            var handle = YooAssets.LoadRawFileSync(path);
            try
            {
                return YooAssetHandleAwaiter.IsSucceed(handle) ? handle.GetRawFilePath() : null;
            }
            finally
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
            }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<byte[]> LoadRawAsync(string path, CancellationToken token)
#else
        public async Task<byte[]> LoadRawAsync(string path, CancellationToken token)
#endif
        {
            var handle = YooAssets.LoadRawFileAsync(path);
            try
            {
#if YOKIFRAME_UNITASK_SUPPORT
                await YooAssetHandleAwaiter.WaitAsync(handle, token);
#else
                await YooAssetHandleAwaiter.WaitAsync(handle, token).ConfigureAwait(false);
#endif
                return YooAssetHandleAwaiter.IsSucceed(handle) ? handle.GetRawFileData() : null;
            }
            finally
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
            }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<string> LoadRawTextAsync(string path, CancellationToken token)
#else
        public async Task<string> LoadRawTextAsync(string path, CancellationToken token)
#endif
        {
            var handle = YooAssets.LoadRawFileAsync(path);
            try
            {
#if YOKIFRAME_UNITASK_SUPPORT
                await YooAssetHandleAwaiter.WaitAsync(handle, token);
#else
                await YooAssetHandleAwaiter.WaitAsync(handle, token).ConfigureAwait(false);
#endif
                return YooAssetHandleAwaiter.IsSucceed(handle) ? handle.GetRawFileText() : null;
            }
            finally
            {
                YooAssetHandleAwaiter.ReleaseQuietly(handle);
            }
        }
    }
}
#endif
#endif
