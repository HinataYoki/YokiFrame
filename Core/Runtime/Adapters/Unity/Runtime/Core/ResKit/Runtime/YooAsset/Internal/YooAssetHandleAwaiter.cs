#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Threading;
using YooAsset;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame.Unity
{
    internal static class YooAssetHandleAwaiter
    {
#if YOKIFRAME_UNITASK_SUPPORT
        public static async UniTask WaitAsync(HandleBase handle, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                await UniTask.WaitUntil(handle, static h => h.IsDone, cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                ReleaseQuietly(handle);
                throw;
            }

            ThrowIfFailed(handle);
        }
#else
        public static async Task WaitAsync(HandleBase handle, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                token.ThrowIfCancellationRequested();

            try
            {
                while (!handle.IsDone)
                {
                    if (token.IsCancellationRequested)
                        token.ThrowIfCancellationRequested();

                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
                ReleaseQuietly(handle);
                throw;
            }

            ThrowIfFailed(handle);
        }
#endif

        public static bool IsSucceed(HandleBase handle)
        {
            return handle != null && IsSuccessStatus(handle);
        }

        public static void ThrowIfFailed(HandleBase handle)
        {
            if (handle == null || IsSuccessStatus(handle))
                return;

            throw new InvalidOperationException("YooAsset load failed: " + GetErrorMessage(handle));
        }

        public static void ReleaseQuietly(HandleBase handle)
        {
            if (handle == null)
                return;

            try
            {
                handle.Release();
            }
            catch
            {
                // 释放路径不能掩盖原始加载/取消异常。
            }
        }

        private static bool IsSuccessStatus(HandleBase handle)
        {
#if YOOASSET_3_0_OR_NEWER
            return handle.Status == EOperationStatus.Succeeded;
#else
            return handle.Status == EOperationStatus.Succeed;
#endif
        }

        private static string GetErrorMessage(HandleBase handle)
        {
#if YOOASSET_3_0_OR_NEWER
            return handle.Error;
#else
            return handle.LastError;
#endif
        }
    }
}
#endif
#endif
