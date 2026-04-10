#if YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// UniTask 异步任务 Action
    /// 支持 CancellationToken 取消和 PlayerLoopTiming 控制
    /// </summary>
    internal class UniTaskAction : ActionBase
    {
        private Func<CancellationToken, UniTask> mTaskGetter;
        private CancellationTokenSource mCts;
        private static readonly SimplePoolKit<UniTaskAction> mPool = new(() => new UniTaskAction());

        static UniTaskAction()
        {
            ActionKitPlayerLoopSystem.RegisterRecycleProcessor<UniTaskAction>();
        }

        public static UniTaskAction Allocate(Func<CancellationToken, UniTask> taskGetter)
        {
            var action = mPool.Allocate();
            action.ActionID = ActionKit.ID_GENERATOR++;
            action.Deinited = false;
            action.OnInit();
            action.mTaskGetter = taskGetter;
            return action;
        }

        public static UniTaskAction Allocate(Func<UniTask> taskGetter)
        {
            return Allocate(_ => taskGetter());
        }

        public override void OnStart()
        {
            mCts = new CancellationTokenSource();
            ExecuteTaskSafe(mCts.Token).Forget();
        }

        private async UniTaskVoid ExecuteTaskSafe(CancellationToken token)
        {
            try
            {
                await mTaskGetter(token);
                if (!token.IsCancellationRequested)
                {
                    this.Finish();
                }
            }
            catch (OperationCanceledException)
            {
                // 任务被取消，正常结束
            }
            catch (Exception e)
            {
                KitLogger.Error($"[UniTaskAction] 执行异常: {e.Message}\n{e.StackTrace}");
                this.Finish();
            }
        }

        public override void OnDeinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                mCts?.Cancel();
                mCts?.Dispose();
                mCts = null;
                mTaskGetter = null;
                ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<UniTaskAction>(mPool, this));
            }
        }

        public override string GetDebugInfo() =>
            mTaskGetter != null ? $"UniTaskAction -> {mTaskGetter.Method.DeclaringType}.{mTaskGetter.Method.Name}" : "UniTaskAction";
    }

    public static class UniTaskActionExtension
    {
        /// <summary>
        /// 添加 UniTask 到序列
        /// </summary>
        public static ISequence UniTask(this ISequence self, Func<UniTask> taskGetter)
        {
            return self.Append(UniTaskAction.Allocate(taskGetter));
        }

        /// <summary>
        /// 添加支持取消的 UniTask 到序列
        /// </summary>
        public static ISequence UniTask(this ISequence self, Func<CancellationToken, UniTask> taskGetter)
        {
            return self.Append(UniTaskAction.Allocate(taskGetter));
        }

        /// <summary>
        /// 将 UniTask 转换为 IAction
        /// </summary>
        public static IAction ToAction(this UniTask task)
        {
            return UniTaskAction.Allocate(() => task);
        }
    }
}
#endif
