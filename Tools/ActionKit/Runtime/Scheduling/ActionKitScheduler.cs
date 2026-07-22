using System;
using System.Collections.Generic;
#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Diagnostics;
#endif
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// 在宿主线程推进根 Action，集中处理完成、取消、故障、清理和延迟回池。
    /// </summary>
    public static partial class ActionKitScheduler
    {
#if UNITY_EDITOR || (GODOT && TOOLS)
        private const int DIAGNOSTIC_SAMPLE_FRAME_INTERVAL = 6;
#endif
        private static readonly object sPrepareSyncRoot = new();
        private static readonly List<ActionController> sPrepared = new(32);
        private static readonly List<ActionController> sExecuting = new(64);
        private static readonly List<IPooledAction> sPendingRecycle = new(128);
        private static readonly SchedulerUpdateListener sUpdateListener = new();
        private static ActionController sAdvancingController;
        private static long sActionId;
#if UNITY_EDITOR || (GODOT && TOOLS)
        private static long sDiagnosticVersion;
#endif
        private static int sTickActive;
        private static volatile bool sInitialized;

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>获取已经由宿主投递的帧数，使用 long 避免长期运行溢出。</summary>
        public static long FrameCount { get; private set; }

        /// <summary>获取正常完成的根 Action 累计数量。</summary>
        public static long FinishedCount { get; private set; }

        /// <summary>获取取消的根 Action 累计数量。</summary>
        public static long CancelledCount { get; private set; }

        /// <summary>获取因生命周期异常结束的根 Action 累计数量。</summary>
        public static long FaultedCount { get; private set; }

        /// <summary>获取当前已准备或正在执行的根 Action 数量。</summary>
        public static int ExecutingCount
        {
            get { lock (sPrepareSyncRoot) return sPrepared.Count + sExecuting.Count; }
        }

        /// <summary>获取 ActionKit 结构、终态和有界进度采样的单调诊断版本。</summary>
        public static long DiagnosticVersion => Interlocked.Read(ref sDiagnosticVersion);
#endif

        /// <summary>
        /// 将调度器注册到 Core 帧派发器；重复调用保持幂等。
        /// </summary>
        public static void Initialize()
        {
            if (sInitialized) return;
            lock (sPrepareSyncRoot)
            {
                if (sInitialized) return;
                YokiFrameUpdateDispatcher.Register(sUpdateListener);
                sInitialized = true;
            }
        }

        /// <summary>
        /// 为内置 Action 或首次启动的自定义 Action 分配非零单调 ID。
        /// </summary>
        /// <returns>新的 Action ID。</returns>
        internal static ulong NextActionId()
        {
            Initialize();
            long actionId = Interlocked.Increment(ref sActionId);
            if (actionId <= 0) throw new OverflowException("ActionKit action id space is exhausted.");
            return unchecked((ulong)actionId);
        }

        /// <summary>
        /// 同步执行根 Action 的 dt=0 首推，未结束时进入下一宿主 Tick 的准备队列。
        /// </summary>
        /// <param name="action">待启动根 Action。</param>
        /// <param name="onFinish">仅在正常完成时调用的 controller 回调。</param>
        /// <returns>不会被复用给其它动作的稳定 controller handle。</returns>
        internal static IActionController Execute(IAction action, Action<IActionController> onFinish)
        {
            EnsureHostThread();
            Initialize();
            ActionOwnership.ClaimRoot(action);
            ActionController controller = null;
            try
            {
                PrepareAction(action);
                ActionRuntime.PrepareTreeActionIds(action);
                controller = new ActionController(action, onFinish);
                RegisterActionIds(controller);
#if UNITY_EDITOR || (GODOT && TOOLS)
                RegisterStackTrace(action.ActionID);
                if (ActionStackTraceService.Enabled) controller.MarkStackTraceRegistered();
#endif
                if (!AdvanceController(controller, 0f)) AddPrepared(controller);
                return controller;
            }
            catch
            {
                if (controller != null && controller.Action != null) FinalizeWithoutCallbacks(controller);
                else ActionOwnership.Release(action);
                throw;
            }
        }

        /// <summary>
        /// 由测试、自定义宿主或 Core 帧监听者推进一次；调用方必须在同一宿主线程串行调用。
        /// </summary>
        /// <param name="scaledDeltaTime">缩放时间步长。</param>
        /// <param name="unscaledDeltaTime">非缩放时间步长。</param>
        public static void Tick(float scaledDeltaTime, float unscaledDeltaTime)
        {
            EnsureHostThread();
            if (Interlocked.CompareExchange(ref sTickActive, 1, 0) != 0)
                throw new InvalidOperationException("ActionKitScheduler.Tick cannot be called reentrantly.");
            try
            {
                ValidateDeltaTime(scaledDeltaTime, nameof(scaledDeltaTime));
                ValidateDeltaTime(unscaledDeltaTime, nameof(unscaledDeltaTime));
                lock (sPrepareSyncRoot)
                {
#if UNITY_EDITOR || (GODOT && TOOLS)
                    FrameCount++;
#endif
                    MovePreparedToExecuting();
                    CompactExecuting(scaledDeltaTime, unscaledDeltaTime);
#if UNITY_EDITOR || (GODOT && TOOLS)
                    if (sExecuting.Count > 0 && FrameCount % DIAGNOSTIC_SAMPLE_FRAME_INTERVAL == 0)
                        NotifyStateChanged();
#endif
                }
            }
            finally
            {
                Volatile.Write(ref sTickActive, 0);
            }
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 将当前准备和执行 controller 写入调用方复用列表，不在诊断调用内创建集合。
        /// </summary>
        /// <param name="result">接收稳定 handle 的可复用列表。</param>
        public static void GetExecutingActionControllers(List<IActionController> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            lock (sPrepareSyncRoot)
            {
                for (var index = 0; index < sPrepared.Count; index++) result.Add(sPrepared[index]);
                for (var index = 0; index < sExecuting.Count; index++) result.Add(sExecuting[index]);
            }
        }

        /// <summary>
        /// 将当前准备和执行根 Action 写入调用方复用列表。
        /// </summary>
        /// <param name="result">接收根 Action 的可复用列表。</param>
        public static void GetExecutingActions(List<IAction> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            lock (sPrepareSyncRoot)
            {
                for (var index = 0; index < sPrepared.Count; index++)
                    if (sPrepared[index].Action != null) result.Add(sPrepared[index].Action);
                for (var index = 0; index < sExecuting.Count; index++)
                    if (sExecuting[index].Action != null) result.Add(sExecuting[index].Action);
            }
        }

        /// <summary>递增诊断版本；显式状态变化调用，不创建事件或闭包。</summary>
        internal static void NotifyStateChanged() => Interlocked.Increment(ref sDiagnosticVersion);
#endif

        /// <summary>为根 Action 准备新的执行轮次；内置池租约保留 Allocate 时分配的 ID。</summary>
        private static void PrepareAction(IAction action)
        {
            if (action is ActionBase actionBase)
            {
                ulong actionId = action.ActionID == 0 ? NextActionId() : action.ActionID;
                actionBase.PrepareExecution(actionId);
                return;
            }

            if (action.ActionID == 0)
                throw new InvalidOperationException("A custom IAction that does not derive from ActionBase must provide a non-zero ActionID.");
            ActionRuntime.PrepareExternalExecution(action);
        }

        /// <summary>关闭 fluent 配置失败后尚未调度的节点，并延迟到安全回收阶段归还 PoolKit。</summary>
        /// <param name="action">当前扩展刚创建、但未成功交给父容器的 Action。</param>
        internal static void DiscardUnscheduled(IAction action)
        {
            if (action == null || ActionOwnership.IsActiveRoot(action)) return;
            lock (sPrepareSyncRoot)
            {
                action.ActionState = ActionStatus.Finished;
                ActionRuntime.DeinitializeTree(action, sPendingRecycle);
#if UNITY_EDITOR || (GODOT && TOOLS)
                NotifyStateChanged();
#endif
            }
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>在启用诊断时捕获 Start 调用堆栈。</summary>
        private static void RegisterStackTrace(ulong actionId)
        {
            if (ActionStackTraceService.Enabled)
                ActionStackTraceService.Register(actionId, new StackTrace(3, true));
        }
#endif

        /// <summary>把未完成根加入准备队列，使 Tick 迭代期间启动的新动作延后到下一帧。</summary>
        private static void AddPrepared(ActionController controller)
        {
            lock (sPrepareSyncRoot) sPrepared.Add(controller);
#if UNITY_EDITOR || (GODOT && TOOLS)
            NotifyStateChanged();
#endif
        }

        /// <summary>把本帧开始前的准备队列移动到执行列表。</summary>
        private static void MovePreparedToExecuting()
        {
            lock (sPrepareSyncRoot)
            {
                if (sPrepared.Count == 0) return;
                sExecuting.AddRange(sPrepared);
                sPrepared.Clear();
                if (sPrepared.Capacity > MAX_RETAINED_PREPARED_CAPACITY)
                    sPrepared.Capacity = MAX_RETAINED_PREPARED_CAPACITY;
            }
        }

        /// <summary>原地压缩执行列表；终结 controller 不创建移除快照。</summary>
        private static void CompactExecuting(float scaledDeltaTime, float unscaledDeltaTime)
        {
            var writeIndex = 0;
            for (var readIndex = 0; readIndex < sExecuting.Count; readIndex++)
            {
                ActionController controller = sExecuting[readIndex];
                float deltaTime = controller.UpdateMode == ActionUpdateModes.ScaledDeltaTime
                    ? scaledDeltaTime : unscaledDeltaTime;
                if (!AdvanceController(controller, deltaTime)) sExecuting[writeIndex++] = controller;
            }

            if (writeIndex < sExecuting.Count) sExecuting.RemoveRange(writeIndex, sExecuting.Count - writeIndex);
            if (sExecuting.Count <= MAX_RETAINED_EXECUTING_CAPACITY
                && sExecuting.Capacity > MAX_RETAINED_EXECUTING_CAPACITY)
                sExecuting.Capacity = MAX_RETAINED_EXECUTING_CAPACITY;
        }

        /// <summary>推进一个 controller，并把所有异常转为一次 Faulted 终态。</summary>
        private static bool AdvanceController(ActionController controller, float deltaTime)
        {
            if (controller.TryTakePendingFault(out Exception pendingFault))
            {
                FinalizeFaulted(controller, pendingFault);
                return true;
            }

            if (controller.CancellationRequested)
            {
                FinalizeCancelled(controller);
                return true;
            }

            try
            {
                bool completed;
                ActionController previousController = sAdvancingController;
                sAdvancingController = controller;
                try
                {
                    completed = ActionRuntime.Update(controller.Action, deltaTime);
                }
                finally
                {
                    sAdvancingController = previousController;
                }
                if (controller.TryTakePendingFault(out pendingFault))
                    FinalizeFaulted(controller, pendingFault);
                else if (completed) FinalizeCompleted(controller);
                else if (controller.CancellationRequested) FinalizeCancelled(controller);
                else return false;
            }
            catch (Exception exception)
            {
                FinalizeFaulted(controller, exception);
            }
            return true;
        }

        /// <summary>执行正常完成回调；回调异常会把当前根改记为 Faulted。</summary>
        private static void FinalizeCompleted(ActionController controller)
        {
            controller.MarkCompleted();
            try
            {
                controller.Finish?.Invoke(controller);
            }
            catch (Exception exception)
            {
                FinalizeFaulted(controller, exception);
                return;
            }

            IAction action = controller.Action;
            Exception cleanupException = FinalizeTree(controller);
            if (cleanupException != null)
            {
                controller.MarkFaulted();
                RecordFaultedTerminal(controller, action, cleanupException);
                return;
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            FinishedCount++;
            RecordTerminalSafely(action, ActionKitTerminalOutcome.Completed);
#endif
        }

        /// <summary>完成取消终态，不调用 Action.OnFinish 或 controller Finish。</summary>
        private static void FinalizeCancelled(ActionController controller)
        {
            controller.MarkCancelled();
            IAction action = controller.Action;
            Exception cleanupException = FinalizeTree(controller);
            if (cleanupException != null)
            {
                controller.MarkFaulted();
                RecordFaultedTerminal(controller, action, cleanupException);
                return;
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            CancelledCount++;
            RecordTerminalSafely(action, ActionKitTerminalOutcome.Cancelled);
#endif
        }

        /// <summary>记录一次故障并释放完整树，不把异常伪装为正常完成。</summary>
        private static void FinalizeFaulted(ActionController controller, Exception exception)
        {
            controller.MarkFaulted();
            IAction action = controller.Action;
            Exception cleanupException = FinalizeTree(controller);
            if (cleanupException != null && !ReferenceEquals(cleanupException, exception))
                ActionKitFailureReporter.TryLog("[ActionKit] Fault cleanup also failed: ", cleanupException);
            RecordFaultedTerminal(controller, action, exception);
        }

        /// <summary>记录唯一 Faulted 计数、历史和 best-effort 错误日志。</summary>
        private static void RecordFaultedTerminal(
            ActionController controller,
            IAction action,
            Exception exception)
        {
#if UNITY_EDITOR || (GODOT && TOOLS)
            FaultedCount++;
            RecordTerminalSafely(action, ActionKitTerminalOutcome.Faulted, exception);
#endif
            ActionKitFailureReporter.TryLog(
                "[ActionKit] Action " + controller.CurExecuteActionID + " faulted: ",
                exception);
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>尽力写入终态历史，诊断失败不得改变已经确定的生命周期结果。</summary>
        private static void RecordTerminalSafely(
            IAction action,
            ActionKitTerminalOutcome outcome,
            Exception exception = null)
        {
            try
            {
                ActionKitDiagnosticHistory.Record(action, outcome, exception);
            }
            catch (Exception diagnosticException)
            {
                ActionKitFailureReporter.TryLog(
                    "[ActionKit] Terminal history recording failed: ",
                diagnosticException);
            }
        }
#endif

        /// <summary>宿主重置时释放树且不执行用户完成回调；计数随后整体清零。</summary>
        private static void FinalizeWithoutCallbacks(ActionController controller)
        {
            if (controller == null || controller.Action == null) return;
            controller.MarkCancelled();
            Exception cleanupException = FinalizeTree(controller);
            if (cleanupException != null)
            {
                controller.MarkFaulted();
                ActionKitFailureReporter.TryLog("[ActionKit] Host reset cleanup failed: ", cleanupException);
            }
        }

        /// <summary>从诊断移除根并按子到父顺序 Deinit，返回首个清理异常且始终断开 handle。</summary>
        private static Exception FinalizeTree(ActionController controller)
        {
            IAction action = controller.Action;
            if (action == null) return null;
            Exception firstException = null;
            try
            {
                try
                {
                    UnregisterActionIds(controller, action);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                    ActionKitFailureReporter.TryLog("[ActionKit] Action ID cleanup failed: ", exception);
                }

#if UNITY_EDITOR || (GODOT && TOOLS)
                try
                {
                    if (controller.TryClearStackTraceRegistered())
                        ActionStackTraceService.Remove(controller.CurExecuteActionID);
                }
                catch (Exception exception)
                {
                    ActionKitFailureReporter.TryLog("[ActionKit] Stack trace cleanup failed: ", exception);
                }
#endif

                try
                {
                    Exception treeException = ActionRuntime.DeinitializeTree(action, sPendingRecycle);
                    if (firstException == null) firstException = treeException;
                }
                catch (Exception exception)
                {
                    if (firstException == null) firstException = exception;
                    ActionKitFailureReporter.TryLog("[ActionKit] Action tree cleanup failed: ", exception);
                }
            }
            finally
            {
                controller.DetachAction();
#if UNITY_EDITOR || (GODOT && TOOLS)
                NotifyStateChanged();
#endif
            }

            return firstException;
        }

        /// <summary>拒绝负数、NaN 和无穷 Tick 时间。</summary>
        private static void ValidateDeltaTime(float deltaTime, string parameterName)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(parameterName);
        }

        /// <summary>把 Core FrameLoop 帧与宿主重置转发给静态 Scheduler。</summary>
        private sealed class SchedulerUpdateListener : IYokiFrameUpdateListener
        {
            /// <summary>推进 ActionKit 并在同一宿主帧末回收已释放内置节点。</summary>
            public void OnFrameUpdate(float scaledDeltaTime, float unscaledDeltaTime)
            {
                Tick(scaledDeltaTime, unscaledDeltaTime);
                ProcessRecycle();
            }

            /// <summary>宿主代际结束时关闭全部活动动作。</summary>
            public void OnHostReset() => Cleanup();
        }
    }
}
