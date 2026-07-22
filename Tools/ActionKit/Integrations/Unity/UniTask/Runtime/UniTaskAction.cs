#if UNITY_5_3_OR_NEWER && YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>在 ActionKit 宿主 Tick 上观察 UniTask，并为 token factory 提供结构化取消所有权。</summary>
    internal sealed class UniTaskAction : ActionBase, IPooledAction
    {
        private static readonly ObjectPool<UniTaskAction> sPool = PoolKit.Create(
            static () => new UniTaskAction(), null, static action => action.ResetForPool(), ActionPoolSettings.Default);
        private Func<UniTask> mTaskFactory;
        private Func<CancellationToken, UniTask> mCancelableTaskFactory;
        private CancellationTokenSource mCancellationSource;
        private UniTask mTask;
        private bool mHasTask;
        private bool mCompletionObserved;
        private bool mRoundStarted;
        private bool mDirectTask;
        private bool mDirectTaskConsumed;

        /// <summary>限制实例只能由静态 Allocate 与 Core PoolKit 创建。</summary>
        private UniTaskAction() { }

        /// <summary>分配每轮调用无 token factory 的 Action。</summary>
        /// <param name="taskFactory">每次执行轮次创建 UniTask 的 factory。</param>
        /// <returns>新的 UniTask Action 租约。</returns>
        internal static UniTaskAction Allocate(Func<UniTask> taskFactory)
        {
            if (taskFactory == null) throw new ArgumentNullException(nameof(taskFactory));
            UniTaskAction action = AllocateCore();
            action.mTaskFactory = taskFactory;
            return action;
        }

        /// <summary>分配每轮获得 ActionKit 取消 token 的 Action。</summary>
        /// <param name="taskFactory">接收当前轮取消 token 的 UniTask factory。</param>
        /// <returns>新的 UniTask Action 租约。</returns>
        internal static UniTaskAction Allocate(Func<CancellationToken, UniTask> taskFactory)
        {
            if (taskFactory == null) throw new ArgumentNullException(nameof(taskFactory));
            UniTaskAction action = AllocateCore();
            action.mCancelableTaskFactory = taskFactory;
            return action;
        }

        /// <summary>直接包装一个一次性 UniTask source，不创建捕获闭包。</summary>
        /// <param name="task">待观察的一次性 UniTask。</param>
        /// <returns>新的 UniTask Action 租约。</returns>
        internal static UniTaskAction Allocate(UniTask task)
        {
            UniTaskAction action = AllocateCore();
            action.mTask = task;
            action.mHasTask = true;
            action.mDirectTask = true;
            return action;
        }

        /// <summary>Repeat 新一轮前取消并观察上一轮仍活动的 UniTask，禁止异步资源跨轮泄漏。</summary>
        public override void OnInit()
        {
            base.OnInit();
            if (!mRoundStarted) return;
            CloseRound(true);
            mRoundStarted = false;
        }

        /// <summary>创建当前轮 UniTask，并同步观察已经完成的 source。</summary>
        public override void OnStart()
        {
            if (mDirectTask)
            {
                if (mDirectTaskConsumed)
                    throw new InvalidOperationException("A direct UniTask can only be consumed once; use a factory inside Repeat.");
                mDirectTaskConsumed = true;
            }
            else
            {
                CreateFactoryTask();
            }

            mRoundStarted = true;
            ObserveCompletion();
        }

        /// <summary>每个宿主 Tick 只读取 UniTask awaiter 状态，完成时在宿主线程解析终态。</summary>
        /// <param name="dt">UniTask 不直接消费的 ActionKit 时间步长。</param>
        public override void OnExecute(float dt) => ObserveCompletion();

        /// <summary>取消、故障或宿主重置时传播 token，并转移仍活动 source 的异常观察权。</summary>
        public override void OnDeinit()
        {
            CloseRound(true);
            mTaskFactory = null;
            mCancelableTaskFactory = null;
            mDirectTask = false;
            mDirectTaskConsumed = false;
            mRoundStarted = false;
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>创建当前 UniTask 是否存在及是否完成的按需诊断摘要。</summary>
        /// <returns>不消费 UniTask source 的状态文本。</returns>
        public override string GetDebugInfo()
        {
            if (!mHasTask) return "UniTaskAction";
            return "UniTaskAction(completed=" + mTask.GetAwaiter().IsCompleted + ")";
        }
#endif

        /// <summary>把完成清理的租约交还零保留 PoolKit。</summary>
        void IPooledAction.ReturnToPool() => sPool.Recycle(this);

        /// <summary>从局部 PoolKit 分配实例并安装新的 Action ID。</summary>
        /// <returns>已重置公共租约状态的 Action。</returns>
        private static UniTaskAction AllocateCore()
        {
            UniTaskAction action = sPool.Allocate();
            action.PreparePooled(ActionKitScheduler.NextActionId());
            return action;
        }

        /// <summary>根据当前 factory 形态创建一次 UniTask；token source 只为可取消入口分配。</summary>
        private void CreateFactoryTask()
        {
            if (mCancelableTaskFactory != null)
            {
                mCancellationSource = new CancellationTokenSource();
                mTask = mCancelableTaskFactory(mCancellationSource.Token);
            }
            else
            {
                mTask = mTaskFactory();
            }

            mHasTask = true;
            mCompletionObserved = false;
        }

        /// <summary>观察 UniTask 完成、取消或 fault；GetResult 只允许执行一次。</summary>
        private void ObserveCompletion()
        {
            if (!mHasTask || mCompletionObserved) return;
            UniTask.Awaiter awaiter = mTask.GetAwaiter();
            if (!awaiter.IsCompleted) return;

            mCompletionObserved = true;
            awaiter.GetResult();
            this.Finish();
        }

        /// <summary>闭合当前轮资源；取消回调异常仍会在完成清理后形成 Faulted。</summary>
        /// <param name="requestCancellation">是否向 token factory 请求取消。</param>
        private void CloseRound(bool requestCancellation)
        {
            Exception firstException = null;
            CancellationTokenSource cancellationSource = mCancellationSource;
            mCancellationSource = null;
            bool needsCancellation = !mHasTask || !mCompletionObserved;
            if (requestCancellation
                && needsCancellation
                && cancellationSource != null
                && !cancellationSource.IsCancellationRequested)
            {
                try { cancellationSource.Cancel(); }
                catch (Exception exception) { firstException = exception; }
            }

            if (mHasTask && !mCompletionObserved)
            {
                ObserveDetached(mTask, cancellationSource).Forget();
                cancellationSource = null;
            }

            if (cancellationSource != null)
            {
                try { cancellationSource.Dispose(); }
                catch (Exception exception) { if (firstException == null) firstException = exception; }
            }

            mTask = default;
            mHasTask = false;
            mCompletionObserved = false;
            if (firstException != null) ExceptionDispatchInfo.Capture(firstException).Throw();
        }

        /// <summary>在 Action 租约结束后继续观察唯一 UniTask source，并在真实终态后释放 token source。</summary>
        /// <param name="task">已经移交观察权的 UniTask。</param>
        /// <param name="cancellationSource">需延长到 UniTask 终态的 token source。</param>
        private static async UniTaskVoid ObserveDetached(UniTask task, CancellationTokenSource cancellationSource)
        {
            try { await task; }
            catch (OperationCanceledException) { return; }
            catch (Exception exception) { ReportDetachedFailure(exception); }
            finally { cancellationSource?.Dispose(); }
        }

        /// <summary>报告已经脱离 Action 租约的异步 fault；正常取消不产生 Error。</summary>
        /// <param name="exception">UniTask 在租约结束后产生的异常。</param>
        private static void ReportDetachedFailure(Exception exception)
        {
            if (exception is OperationCanceledException) return;
            ActionKitFailureReporter.TryLog("[ActionKit] Detached UniTask faulted: ", exception);
        }

        /// <summary>回池前清除 factory、token source 与 UniTask source 引用。</summary>
        private void ResetForPool()
        {
            mTaskFactory = null;
            mCancelableTaskFactory = null;
            mCancellationSource = null;
            mTask = default;
            mHasTask = false;
            mCompletionObserved = false;
            mRoundStarted = false;
            mDirectTask = false;
            mDirectTaskConsumed = false;
        }
    }
}
#endif
