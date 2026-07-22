#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif
using YooAsset;

namespace YokiFrame.Unity
{
    /// <summary>统一等待 YooAsset V2/V3 操作，并把失败状态转换为异常。</summary>
    internal static class YooAssetOperationAwaiter
    {
#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>逐帧等待 YooAsset 操作完成，并响应 Unity 生命周期取消。</summary>
        internal static async UniTask WaitAsync(AsyncOperationBase operation, CancellationToken token)
#else
        /// <summary>逐帧等待 YooAsset 操作完成，并响应调用方取消。</summary>
        internal static async Task WaitAsync(AsyncOperationBase operation, CancellationToken token)
#endif
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            while (!operation.IsDone)
            {
                token.ThrowIfCancellationRequested();
#if YOKIFRAME_UNITASK_SUPPORT
                await UniTask.Yield(PlayerLoopTiming.Update, token);
#else
                await Task.Yield();
#endif
            }

            token.ThrowIfCancellationRequested();
            ThrowIfFailed(operation);
        }

        /// <summary>在 YooAsset 操作失败时抛出包含原始错误的异常。</summary>
        private static void ThrowIfFailed(AsyncOperationBase operation)
        {
#if YOKIFRAME_YOOASSET_3
            bool succeeded = operation.Status == EOperationStatus.Succeeded;
#else
            bool succeeded = operation.Status == EOperationStatus.Succeed;
#endif
            if (!succeeded)
                throw new InvalidOperationException("YooAsset operation failed: " + operation.Error);
        }
    }
}
#endif
