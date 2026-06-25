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
    internal static class YooInitOperationAwaiter
    {
#if YOKIFRAME_UNITASK_SUPPORT
        public static async UniTask WaitAsync(AsyncOperationBase operation, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            await UniTask.WaitUntil(operation, static op => op.IsDone, cancellationToken: token);
            ThrowIfFailed(operation);
        }
#else
        public static async Task WaitAsync(AsyncOperationBase operation, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                token.ThrowIfCancellationRequested();

            while (!operation.IsDone)
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();

                await Task.Yield();
            }

            ThrowIfFailed(operation);
        }
#endif

        public static void ThrowIfFailed(AsyncOperationBase operation)
        {
            if (operation == null || IsSuccess(operation))
                return;

            throw new InvalidOperationException("YooAsset operation failed: " + operation.Error);
        }

        public static bool IsSuccess(AsyncOperationBase operation)
        {
#if YOOASSET_3_0_OR_NEWER
            return operation.Status == EOperationStatus.Succeeded;
#else
            return operation.Status == EOperationStatus.Succeed;
#endif
        }
    }
}
#endif
#endif
