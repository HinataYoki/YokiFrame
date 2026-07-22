#if UNITY_2022_3_OR_NEWER
using System;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// 合并同类型面板物化请求，并为每个等待者保留独立取消语义。
    /// </summary>
    internal sealed class PanelLoadOperation : IDisposable
    {
        private readonly CancellationTokenSource mSharedCancellation = new();
        private readonly TaskCompletionSource<PanelEntry> mCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Action<PanelLoadOperation> mAbandonedCallback;
        private int mWaiterCount;
        private bool mCompleted;
        private bool mMaterializationCompleted;
        private bool mDisposed;

        /// <summary>
        /// 创建指定面板类型的共享物化操作。
        /// </summary>
        internal PanelLoadOperation(Type panelType, Action<PanelLoadOperation> abandonedCallback)
            : this(panelType, 0L, abandonedCallback)
        {
        }

        /// <summary>
        /// 创建绑定当前 UIKit controller 生命周期代次的共享物化操作。
        /// </summary>
        /// <param name="panelType">需要物化的面板类型。</param>
        /// <param name="generation">创建该操作时的 controller 生命周期代次。</param>
        /// <param name="abandonedCallback">最后一个等待者离开时的回调。</param>
        internal PanelLoadOperation(
            Type panelType,
            long generation,
            Action<PanelLoadOperation> abandonedCallback)
        {
            PanelType = panelType ?? throw new ArgumentNullException(nameof(panelType));
            Generation = generation;
            mAbandonedCallback = abandonedCallback
                ?? throw new ArgumentNullException(nameof(abandonedCallback));
        }

        internal Type PanelType { get; }
        internal long Generation { get; }
        internal CancellationToken SharedToken => mSharedCancellation.Token;
        internal IUIData InitializationData { get; private set; }
        internal Task<PanelEntry> Task => mCompletion.Task;

        /// <summary>
        /// 以成功结果完成共享任务；完成前 Task 已始终对重入等待者可见。
        /// </summary>
        internal void SetResult(PanelEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            Complete(() => mCompletion.TrySetResult(entry));
        }

        /// <summary>
        /// 以失败结果完成共享任务，使所有仍在等待的调用方观察同一异常。
        /// </summary>
        internal bool TrySetException(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            return Complete(() => mCompletion.TrySetException(exception));
        }

        /// <summary>
        /// 以取消结果完成共享任务，用于 Root teardown 或全部等待者离开。
        /// </summary>
        internal void SetCanceled()
        {
            Complete(mCompletion.TrySetCanceled);
        }

        /// <summary>
        /// 标记底层 loader 已返回；即使公开等待者已提前取消，也要等到底层结束后再释放共享 CTS。
        /// </summary>
        internal void MarkMaterializationCompleted()
        {
            mMaterializationCompleted = true;
            TryDispose();
        }

        /// <summary>
        /// 加入共享操作；首个非空数据用于实例 OnInit。
        /// </summary>
        internal void Join(IUIData initializationData)
        {
            if (mDisposed) throw new ObjectDisposedException(nameof(PanelLoadOperation));
            mWaiterCount++;
            if (InitializationData == null && initializationData != null) InitializationData = initializationData;
        }

        /// <summary>
        /// 等待共享物化结果；当前令牌取消不会影响其它等待者。
        /// </summary>
        internal async Task<PanelEntry> WaitAsync(CancellationToken token)
        {
            try
            {
                return await AwaitWithCancellationAsync(mCompletion.Task, token);
            }
            finally
            {
                Leave();
            }
        }

        /// <summary>
        /// 主动取消共享底层任务，用于 Root teardown。
        /// </summary>
        internal void Cancel()
        {
            if (!mDisposed && !mSharedCancellation.IsCancellationRequested) mSharedCancellation.Cancel();
        }

        /// <summary>
        /// 幂等释放共享取消源。
        /// </summary>
        public void Dispose()
        {
            if (mDisposed) return;
            mDisposed = true;
            mSharedCancellation.Dispose();
        }

        /// <summary>
        /// 移除一个等待者；全部等待者取消时撤销共享加载。
        /// </summary>
        private void Leave()
        {
            if (mWaiterCount > 0) mWaiterCount--;
            if (mWaiterCount == 0 && !mCompleted) mAbandonedCallback(this);
            TryDispose();
        }

        /// <summary>
        /// 先提交完成标记，再完成公开 Task，避免异步 continuation 把已完成操作误判为 abandoned。
        /// </summary>
        private bool Complete(Func<bool> completion)
        {
            if (mCompleted) return false;
            mCompleted = true;
            bool published = completion();
            TryDispose();
            return published;
        }

        /// <summary>
        /// 仅在任务和所有等待者都结束后释放取消源。
        /// </summary>
        private void TryDispose()
        {
            if (mCompleted && mMaterializationCompleted && mWaiterCount == 0) Dispose();
        }

        /// <summary>
        /// 以独立取消令牌等待任务，避免取消共享底层 Task。
        /// </summary>
        private static async Task<T> AwaitWithCancellationAsync<T>(Task<T> task, CancellationToken token)
        {
            if (!token.CanBeCanceled) return await task;
            token.ThrowIfCancellationRequested();
            var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(static state => ((TaskCompletionSource<bool>)state).TrySetResult(true), canceled))
            {
                Task completed = await System.Threading.Tasks.Task.WhenAny(task, canceled.Task);
                if (ReferenceEquals(completed, canceled.Task)) throw new OperationCanceledException(token);
                return await task;
            }
        }
    }
}
#endif
