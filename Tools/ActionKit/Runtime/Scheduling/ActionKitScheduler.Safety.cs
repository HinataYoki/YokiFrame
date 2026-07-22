using System;
using System.Collections.Generic;
using System.Threading;

namespace YokiFrame
{
    /// <summary>承载 Scheduler 的宿主线程和活动 Action ID 安全边界。</summary>
    public static partial class ActionKitScheduler
    {
        private const int MAX_RETAINED_PREPARED_CAPACITY = 64;
        private const int MAX_RETAINED_EXECUTING_CAPACITY = 128;
        private const int MAX_RETAINED_RECYCLE_CAPACITY = 256;
        private const int MAX_RETAINED_ACTION_ID_CAPACITY = 1024;
        private static readonly HashSet<ulong> sActiveActionIds = new();
        private static readonly HashSet<ulong> sValidationActionIds = new();
        private static int sActiveActionIdHighWatermark;
        private static int sHostThreadId;

        /// <summary>获取当前推进是否已收到取消或控制钩子故障，正常完成钩子必须让位。</summary>
        internal static bool CurrentAdvanceTerminationRequested =>
            sAdvancingController != null
            && (sAdvancingController.CancellationRequested || sAdvancingController.HasPendingFault);

        /// <summary>隔离活动 controller 上下文并手动推进未被调度器持有的 Action。</summary>
        /// <param name="action">已经通过手动更新所有权校验的 Action。</param>
        /// <param name="deltaTime">本次非负时间步长。</param>
        /// <returns>当前 Action 正常完成时返回 true。</returns>
        internal static bool UpdateDetachedAction(IAction action, float deltaTime)
        {
            ActionController previousController = sAdvancingController;
            sAdvancingController = null;
            try
            {
                return ActionRuntime.Update(action, deltaTime);
            }
            finally
            {
                sAdvancingController = previousController;
            }
        }

        /// <summary>
        /// 在同一宿主线程把已完成 OnDeinit 的内置 Action 归还 PoolKit；每帧 Tick 后调用一次。
        /// </summary>
        public static void ProcessRecycle()
        {
            EnsureHostThread();
            if (Volatile.Read(ref sTickActive) != 0)
                throw new InvalidOperationException("ActionKit recycle cannot run during Scheduler Tick.");
            lock (sPrepareSyncRoot)
            {
                for (var index = 0; index < sPendingRecycle.Count; index++)
                {
                    try { sPendingRecycle[index].ReturnToPool(); }
                    catch (Exception exception)
                    {
                        ActionKitFailureReporter.TryLog("[ActionKit] Pool recycle failed: ", exception);
                    }
                }
                sPendingRecycle.Clear();
                if (sPendingRecycle.Capacity > MAX_RETAINED_RECYCLE_CAPACITY)
                    sPendingRecycle.Capacity = MAX_RETAINED_RECYCLE_CAPACITY;
            }
        }

        /// <summary>
        /// 在同一宿主线程取消并释放全部根 Action，清空计数与诊断状态。
        /// </summary>
        public static void Cleanup()
        {
            EnsureHostThread();
            if (Volatile.Read(ref sTickActive) != 0)
                throw new InvalidOperationException("ActionKit cleanup cannot run during Scheduler Tick.");
            lock (sPrepareSyncRoot)
            {
                for (var index = 0; index < sPrepared.Count; index++) FinalizeWithoutCallbacks(sPrepared[index]);
                sPrepared.Clear();
                for (var index = 0; index < sExecuting.Count; index++) FinalizeWithoutCallbacks(sExecuting[index]);
                sExecuting.Clear();
                ProcessRecycle();
                sActiveActionIds.Clear();
                TrimSchedulerBuffersAfterCleanup();
            }
#if UNITY_EDITOR || (GODOT && TOOLS)
            ActionStackTraceService.Clear();
            ActionKitDiagnosticHistory.Clear();
            FrameCount = 0;
            FinishedCount = 0;
            CancelledCount = 0;
            FaultedCount = 0;
            NotifyStateChanged();
#endif
        }

        /// <summary>验证当前调用位于首次调度操作绑定的宿主线程。</summary>
        internal static void EnsureHostThread()
        {
            int currentThreadId = Environment.CurrentManagedThreadId;
            int hostThreadId = Volatile.Read(ref sHostThreadId);
            if (hostThreadId == 0)
            {
                hostThreadId = Interlocked.CompareExchange(
                    ref sHostThreadId,
                    currentThreadId,
                    0);
                if (hostThreadId == 0) hostThreadId = currentThreadId;
            }

            if (hostThreadId != currentThreadId)
                throw new InvalidOperationException("ActionKit scheduling and control must run on one host thread.");
        }

        /// <summary>
        /// 在宿主线程同步终结仍位于准备队列的取消请求，避免下一帧前保留动作树和外部资源。
        /// </summary>
        /// <param name="controller">已经提交取消请求的稳定 controller。</param>
        /// <returns>当前 controller 已从准备队列移除并完成终结时返回 true。</returns>
        internal static bool TryFinalizePreparedCancellation(ActionController controller)
        {
            if (controller == null
                || controller.HasPendingFault
                || !controller.CancellationRequested
                || !IsCurrentHostThread())
                return false;

            lock (sPrepareSyncRoot)
            {
                if (!sPrepared.Remove(controller)) return false;
                FinalizeCancelled(controller);
                return true;
            }
        }

        /// <summary>判断当前线程是否为已经绑定的 Scheduler 宿主线程，不在未绑定线程上隐式建立调度所有权。</summary>
        /// <returns>当前线程能够安全执行 Action 生命周期清理时返回 true。</returns>
        private static bool IsCurrentHostThread()
        {
            int hostThreadId = Volatile.Read(ref sHostThreadId);
            return hostThreadId != 0 && hostThreadId == Environment.CurrentManagedThreadId;
        }

        /// <summary>登记活动树的全部非零 ID，并拒绝树内或活动树之间的重复值。</summary>
        /// <param name="controller">即将进入调度器的稳定根 controller。</param>
        private static void RegisterActionIds(ActionController controller)
        {
            lock (sPrepareSyncRoot)
            {
                sValidationActionIds.Clear();
                var validationCount = 0;
                try
                {
                    CollectActionIds(controller.Action, 0);
                    validationCount = sValidationActionIds.Count;
                    foreach (ulong actionId in sValidationActionIds)
                    {
                        if (sActiveActionIds.Contains(actionId))
                            throw new InvalidOperationException("Action ID is already active: " + actionId);
                    }

                    foreach (ulong actionId in sValidationActionIds) sActiveActionIds.Add(actionId);
                    if (sActiveActionIds.Count > sActiveActionIdHighWatermark)
                        sActiveActionIdHighWatermark = sActiveActionIds.Count;
                    controller.MarkActionIdsRegistered();
                }
                finally
                {
                    if (validationCount == 0) validationCount = sValidationActionIds.Count;
                    sValidationActionIds.Clear();
                    if (validationCount > MAX_RETAINED_ACTION_ID_CAPACITY)
                        sValidationActionIds.TrimExcess();
                }
            }
        }

        /// <summary>在树清理前释放当前 controller 登记的全部活动 ID。</summary>
        /// <param name="controller">即将终结的 controller。</param>
        /// <param name="action">子列表仍完整的动作树根。</param>
        private static void UnregisterActionIds(ActionController controller, IAction action)
        {
            if (!controller.TryClearActionIdsRegistered()) return;
            lock (sPrepareSyncRoot)
            {
                RemoveActionIds(action, 0);
                if (sActiveActionIds.Count == 0
                    && sActiveActionIdHighWatermark > MAX_RETAINED_ACTION_ID_CAPACITY)
                {
                    sActiveActionIds.TrimExcess();
                    sActiveActionIdHighWatermark = 0;
                }
            }
        }

        /// <summary>在宿主清理后释放超出常态阈值的静态缓冲区。</summary>
        private static void TrimSchedulerBuffersAfterCleanup()
        {
            if (sPrepared.Capacity > MAX_RETAINED_PREPARED_CAPACITY)
                sPrepared.Capacity = MAX_RETAINED_PREPARED_CAPACITY;
            if (sExecuting.Capacity > MAX_RETAINED_EXECUTING_CAPACITY)
                sExecuting.Capacity = MAX_RETAINED_EXECUTING_CAPACITY;
            if (sPendingRecycle.Capacity > MAX_RETAINED_RECYCLE_CAPACITY)
                sPendingRecycle.Capacity = MAX_RETAINED_RECYCLE_CAPACITY;
            if (sActiveActionIdHighWatermark > MAX_RETAINED_ACTION_ID_CAPACITY)
            {
                sActiveActionIds.TrimExcess();
                sActiveActionIdHighWatermark = 0;
            }
        }

        /// <summary>收集当前树 ID 到复用校验集合，并拒绝零值和树内重复。</summary>
        private static void CollectActionIds(IAction action, int depth)
        {
            ValidateActionIdDepth(depth);
            if (action == null || action.ActionID == 0)
                throw new InvalidOperationException("Every scheduled Action must have a non-zero Action ID.");
            if (!sValidationActionIds.Add(action.ActionID))
                throw new InvalidOperationException("Action tree contains a duplicate Action ID: " + action.ActionID);
            if (!(action is IActionContainerInternal container)) return;
            for (var index = 0; index < container.ChildCount; index++)
                CollectActionIds(container.GetChild(index), depth + 1);
        }

        /// <summary>递归移除活动树 ID；调用发生在容器 OnDeinit 清空子列表之前。</summary>
        private static void RemoveActionIds(IAction action, int depth)
        {
            ValidateActionIdDepth(depth);
            if (action == null) return;
            sActiveActionIds.Remove(action.ActionID);
            if (!(action is IActionContainerInternal container)) return;
            for (var index = 0; index < container.ChildCount; index++)
                RemoveActionIds(container.GetChild(index), depth + 1);
        }

        /// <summary>复用统一动作树深度契约校验 ID 遍历。</summary>
        private static void ValidateActionIdDepth(int depth)
        {
            if (depth >= ActionTreeLimits.MAX_DEPTH)
                throw new InvalidOperationException("Action tree exceeds the supported ID depth.");
        }
    }
}
