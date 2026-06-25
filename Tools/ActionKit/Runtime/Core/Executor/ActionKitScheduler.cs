using System;
using System.Collections.Generic;
#if YOKIFRAME_ZSTRING_SUPPORT
using Cysharp.Text;
#endif

namespace YokiFrame
{
    /// <summary>
    /// ActionKit 跨引擎调度核心；具体宿主只负责定时调用 Tick。
    /// </summary>
    public static class ActionKitScheduler
    {
        private static readonly object sLock = new();
        private static bool sInitialized;

        private static readonly List<IActionController> sPrepareExecutionActions = new(32);
        private static readonly Dictionary<IAction, IActionController> sExecutingActions = new(64);
        private static readonly List<IActionController> sToActionRemove = new(32);
        private static readonly List<IActionController> sPendingRecycleControllers = new(32);
        private static readonly List<IActionController> sPendingCancelControllers = new(16);
        private static readonly HashSet<IActionController> sPendingCancelSet = new(16);

        private static IActionController[] sExecutingSnapshot = new IActionController[64];

        /// <summary>
        /// 已调度的帧计数。
        /// </summary>
        public static int FrameCount { get; private set; }

        /// <summary>
        /// 已完成的 Action 数量。
        /// </summary>
        public static int FinishedCount { get; private set; }

        /// <summary>
        /// 已取消的 Action 数量。
        /// </summary>
        public static int CancelledCount { get; private set; }

        /// <summary>
        /// 当前正在执行的 Action 数量。
        /// </summary>
        public static int ExecutingCount => sExecutingActions.Count;

        /// <summary>
        /// 初始化 ActionKit 调度器。
        /// </summary>
        public static void Initialize()
        {
            if (sInitialized) return;

            lock (sLock)
            {
                if (sInitialized) return;
                sInitialized = true;
            }
        }

        /// <summary>
        /// 提交一个控制器进入调度队列。
        /// </summary>
        /// <param name="controller">要调度的控制器。</param>
        public static void Execute(IActionController controller)
        {
            Initialize();

            if (controller == null || controller.Action == null) return;

            if (controller.Action.ActionState == ActionStatus.Finished)
                controller.Action.OnInit();

            if (UpdateAction(controller, 0f))
            {
                sPendingRecycleControllers.Add(controller);
                return;
            }

            lock (sPrepareExecutionActions)
            {
                sPrepareExecutionActions.Add(controller);
            }
        }

        /// <summary>
        /// 请求取消一个控制器。
        /// </summary>
        /// <param name="controller">要取消的控制器。</param>
        public static void CancelAction(IActionController controller)
        {
            if (controller == null || controller.Action == null) return;

            lock (sPrepareExecutionActions)
            {
                sPrepareExecutionActions.Remove(controller);
            }

            lock (sPendingCancelControllers)
            {
                if (sPendingCancelSet.Add(controller))
                    sPendingCancelControllers.Add(controller);
            }
        }

        /// <summary>
        /// 注册指定 Action 类型的回收处理器。
        /// </summary>
        public static void RegisterRecycleProcessor<T>()
        {
            Initialize();
            ActionRecyclerManager.RegisterProcessor<T>();
        }

        /// <summary>
        /// 推进 ActionKit 调度。
        /// </summary>
        /// <param name="scaledDeltaTime">宿主缩放时间步长。</param>
        /// <param name="unscaledDeltaTime">宿主非缩放时间步长。</param>
        public static void Tick(float scaledDeltaTime, float unscaledDeltaTime)
        {
            Initialize();
            FrameCount++;
            ProcessPendingCancels();
            MovePreparedActionsToExecuting();
            UpdateExecutingActions(scaledDeltaTime, unscaledDeltaTime);
        }

        /// <summary>
        /// 处理等待回收的 Action 和控制器。
        /// </summary>
        public static void ProcessRecycle()
        {
            ActionRecyclerManager.ProcessAll();

            if (sPendingRecycleControllers.Count == 0) return;

            for (int i = 0; i < sPendingRecycleControllers.Count; i++)
            {
                try
                {
                    var controller = sPendingRecycleControllers[i];
                    if (controller != null)
                        controller.Recycle();
                }
                catch (Exception e)
                {
                    ActionKitRuntimeLog.Error("[ActionKit] 回收 Controller 异常: " + e.Message);
                }
            }
            sPendingRecycleControllers.Clear();
        }

        /// <summary>
        /// 清理调度器所有运行时状态。
        /// </summary>
        public static void Cleanup()
        {
            lock (sLock)
            {
                foreach (var kvp in sExecutingActions)
                {
                    try
                    {
                        if (kvp.Value != null && kvp.Value.Action != null)
                            kvp.Value.Action.OnDeinit();
                    }
                    catch (Exception e)
                    {
                        ActionKitRuntimeLog.Error("[ActionKit] 清理 Action 异常: " + e.Message);
                    }
                }

                sPrepareExecutionActions.Clear();
                sExecutingActions.Clear();
                sToActionRemove.Clear();
                sPendingRecycleControllers.Clear();
                sPendingCancelControllers.Clear();
                sPendingCancelSet.Clear();
                ActionRecyclerManager.EditorCleanupAll();
                FrameCount = 0;
                FinishedCount = 0;
                CancelledCount = 0;
            }
        }

        /// <summary>
        /// 获取当前等待或正在执行的 Action。
        /// </summary>
        /// <param name="result">接收结果的列表。</param>
        public static void GetExecutingActions(List<IAction> result)
        {
            if (result == null) return;

            result.Clear();
            lock (sPrepareExecutionActions)
            {
                for (var i = 0; i < sPrepareExecutionActions.Count; i++)
                {
                    var controller = sPrepareExecutionActions[i];
                    if (controller != null && controller.Action != null)
                        result.Add(controller.Action);
                }
            }

            foreach (var pair in sExecutingActions)
            {
                if (pair.Key != null)
                    result.Add(pair.Key);
            }
        }

        /// <summary>
        /// 获取当前等待或正在执行的控制器。
        /// </summary>
        /// <param name="result">接收结果的列表。</param>
        public static void GetExecutingActionControllers(List<IActionController> result)
        {
            if (result == null) return;

            result.Clear();
            lock (sPrepareExecutionActions)
            {
                for (var i = 0; i < sPrepareExecutionActions.Count; i++)
                {
                    var controller = sPrepareExecutionActions[i];
                    if (controller != null && controller.Action != null)
                        result.Add(controller);
                }
            }

            foreach (var pair in sExecutingActions)
            {
                if (pair.Value != null && pair.Value.Action != null)
                    result.Add(pair.Value);
            }
        }

        private static void ProcessPendingCancels()
        {
            if (sPendingCancelControllers.Count == 0) return;

            lock (sPendingCancelControllers)
            {
                for (int i = 0; i < sPendingCancelControllers.Count; i++)
                {
                    var controller = sPendingCancelControllers[i];
                    if (controller == null || controller.Action == null) continue;

                    if (sExecutingActions.Remove(controller.Action))
                    {
                        try
                        {
                            controller.OnEnd();
                        }
                        catch (Exception e)
                        {
                            ActionKitRuntimeLog.Error("[ActionKit] 取消 Action 异常: " + e.Message);
                        }
                        CancelledCount++;
                        sPendingRecycleControllers.Add(controller);
                    }
                }
                sPendingCancelControllers.Clear();
                sPendingCancelSet.Clear();
            }
        }

        private static void MovePreparedActionsToExecuting()
        {
            if (sPrepareExecutionActions.Count == 0) return;

            lock (sPrepareExecutionActions)
            {
                for (int i = 0; i < sPrepareExecutionActions.Count; i++)
                {
                    var prepareAction = sPrepareExecutionActions[i];
                    if (prepareAction != null && prepareAction.Action != null)
                        sExecutingActions[prepareAction.Action] = prepareAction;
                }
                sPrepareExecutionActions.Clear();
            }
        }

        private static void UpdateExecutingActions(float scaledDeltaTime, float unscaledDeltaTime)
        {
            if (sExecutingActions.Count == 0) return;

            var executingCount = sExecutingActions.Count;
            if (sExecutingSnapshot.Length < executingCount)
                sExecutingSnapshot = new IActionController[executingCount * 2];

            sExecutingActions.Values.CopyTo(sExecutingSnapshot, 0);

            for (int i = 0; i < executingCount; i++)
            {
                var execute = sExecutingSnapshot[i];
                if (execute == null) continue;

                var deltaTime = execute.UpdateMode == ActionUpdateModes.ScaledDeltaTime ? scaledDeltaTime : unscaledDeltaTime;
                if (UpdateAction(execute, deltaTime))
                    sToActionRemove.Add(execute);
            }

            Array.Clear(sExecutingSnapshot, 0, executingCount);

            if (sToActionRemove.Count == 0) return;

            for (int i = 0; i < sToActionRemove.Count; i++)
            {
                var controller = sToActionRemove[i];
                if (controller != null && controller.Action != null)
                {
                    sExecutingActions.Remove(controller.Action);
                    sPendingRecycleControllers.Add(controller);
                }
            }
            sToActionRemove.Clear();
        }

        private static bool UpdateAction(IActionController controller, float dt)
        {
            if (controller == null || controller.Action == null) return true;

            if (controller.IsCancelled)
            {
                try
                {
                    controller.OnEnd();
                }
                catch (Exception e)
                {
                    ActionKitRuntimeLog.Error("[ActionKit] OnEnd 异常: " + e.Message);
                }
                CancelledCount++;
                return true;
            }

            try
            {
                if (controller.Action.Deinited || controller.Action.Update(dt))
                {
                    if (controller.Finish != null)
                        controller.Finish.Invoke(controller);
                    controller.OnEnd();
                    FinishedCount++;
                    return true;
                }
            }
            catch (Exception e)
            {
#if YOKIFRAME_ZSTRING_SUPPORT
                using (var sb = ZString.CreateStringBuilder())
                {
                    sb.Append("[ActionKit] 更新 Action 异常: ");
                    sb.Append(e.Message);
                    sb.Append('\n');
                    sb.Append(controller.Action.GetDebugInfo());
                    ActionKitRuntimeLog.Error(sb.ToString());
                }
#else
                ActionKitRuntimeLog.Error("[ActionKit] 更新 Action 异常: " + e.Message + "\n" + controller.Action.GetDebugInfo());
#endif
                controller.OnEnd();
                FinishedCount++;
                return true;
            }

            return false;
        }
    }
}
