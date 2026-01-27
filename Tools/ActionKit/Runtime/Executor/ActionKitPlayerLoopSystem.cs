using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace YokiFrame
{
    /// <summary>
    /// ActionKit PlayerLoop 驱动系统
    /// 替代 MonoBehaviour Update，实现零 MonoBehaviour 依赖
    /// </summary>
    internal static class ActionKitPlayerLoopSystem
    {
        private struct ActionKitUpdateSystem { }
        private struct ActionKitRecycleSystem { }

        private static bool sInitialized;
        private static readonly object sLock = new();
        
        // 静态缓存委托，避免每次注册时分配
        // 注意：Unity PlayerLoop 的委托调用仍可能产生少量 GC（Unity 内部实现限制）
        private static readonly PlayerLoopSystem.UpdateFunction sCachedUpdateDelegate = UpdateActions;
        private static readonly PlayerLoopSystem.UpdateFunction sCachedRecycleDelegate = ProcessRecycle;

        /// <summary>
        /// 准备执行的任务队列
        /// </summary>
        private static readonly List<IActionController> sPrepareExecutionActions = new(32);
        
        /// <summary>
        /// 正在执行的任务队列
        /// </summary>
        private static readonly Dictionary<IAction, IActionController> sExecutingActions = new(64);
        
        /// <summary>
        /// 已经完成的任务队列（等待移除）
        /// </summary>
        private static readonly List<IActionController> sToActionRemove = new(32);

        /// <summary>
        /// 待回收的 Controller 队列（PreLateUpdate 阶段统一回收）
        /// </summary>
        private static readonly List<IActionController> sPendingRecycleControllers = new(32);

        /// <summary>
        /// 待取消的 Controller 队列（延迟处理，避免遍历冲突）
        /// </summary>
        private static readonly List<IActionController> sPendingCancelControllers = new(16);

        /// <summary>
        /// 待取消的 Controller HashSet（快速去重检查）
        /// </summary>
        private static readonly HashSet<IActionController> sPendingCancelSet = new(16);

        /// <summary>
        /// 执行队列快照缓存（避免每帧分配）
        /// </summary>
        private static IActionController[] sExecutingSnapshot = new IActionController[64];

        /// <summary>
        /// 初始化并注册到 PlayerLoop
        /// </summary>
        public static void Initialize()
        {
            if (sInitialized) return;

            lock (sLock)
            {
                if (sInitialized) return;

                var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
                
                // 注册 Update 系统（在 Update.ScriptRunBehaviourUpdate 之后）
                if (!InsertSystem<Update>(
                    ref playerLoop,
                    typeof(ActionKitUpdateSystem),
                    sCachedUpdateDelegate,
                    insertAfter: typeof(Update.ScriptRunBehaviourUpdate)))
                {
                    Debug.LogWarning("[ActionKit] 注册 PlayerLoop Update 失败");
                }

                // 注册回收系统（在 PreLateUpdate 阶段）
                if (!InsertSystem<PreLateUpdate>(
                    ref playerLoop,
                    typeof(ActionKitRecycleSystem),
                    sCachedRecycleDelegate,
                    insertAfter: null))
                {
                    Debug.LogWarning("[ActionKit] 注册 PlayerLoop Recycle 失败");
                }

                PlayerLoop.SetPlayerLoop(playerLoop);
                sInitialized = true;

#if UNITY_EDITOR
                UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
            }
        }

        /// <summary>
        /// 执行 Action
        /// </summary>
        public static void Execute(IActionController controller)
        {
            Initialize();

            if (controller.Action.ActionState == ActionStatus.Finished)
            {
                controller.Action.OnInit();
            }

            // 尝试立即执行一次
            var dt = controller.UpdateMode is ActionUpdateModes.ScaledDeltaTime 
                ? Time.deltaTime 
                : Time.unscaledDeltaTime;

            if (UpdateAction(controller, dt))
            {
                // 立即完成，加入待回收队列
                sPendingRecycleControllers.Add(controller);
                return;
            }

            lock (sPrepareExecutionActions)
            {
                sPrepareExecutionActions.Add(controller);
            }
        }

        /// <summary>
        /// 取消 Action（提前终止）- 加入待取消队列，延迟处理
        /// </summary>
        public static void CancelAction(IActionController controller)
        {
            if (controller == default || controller.Action == default) return;

            // 从准备队列移除（安全，有锁保护）
            lock (sPrepareExecutionActions)
            {
                sPrepareExecutionActions.Remove(controller);
            }

            // 加入待取消队列，在 UpdateActions 开始时统一处理
            lock (sPendingCancelControllers)
            {
                if (sPendingCancelSet.Add(controller))
                {
                    sPendingCancelControllers.Add(controller);
                }
            }
        }

        /// <summary>
        /// 注册回收处理器（每种 Action 类型调用一次）
        /// </summary>
        internal static void RegisterRecycleProcessor<T>()
        {
            Initialize();
            ActionRecyclerManager.RegisterProcessor<T>();
        }

        #region PlayerLoop 回调

        private static void UpdateActions()
        {
            // 1. 处理待取消的 Controller（在遍历之前）
            if (sPendingCancelControllers.Count > 0)
            {
                lock (sPendingCancelControllers)
                {
                    for (int i = 0; i < sPendingCancelControllers.Count; i++)
                    {
                        var controller = sPendingCancelControllers[i];
                        if (controller != default && controller.Action != default)
                        {
                            if (sExecutingActions.Remove(controller.Action))
                            {
                                try
                                {
                                    controller.OnEnd();
                                }
                                catch (Exception e)
                                {
                                    Debug.LogError($"[ActionKit] 取消 Action 异常: {e.Message}");
                                }
                                sPendingRecycleControllers.Add(controller);
                            }
                        }
                    }
                    sPendingCancelControllers.Clear();
                    sPendingCancelSet.Clear();
                }
            }

            // 2. 从准备队列移入执行队列
            if (sPrepareExecutionActions.Count > 0)
            {
                lock (sPrepareExecutionActions)
                {
                    for (int i = 0; i < sPrepareExecutionActions.Count; i++)
                    {
                        var prepareAction = sPrepareExecutionActions[i];
                        if (prepareAction != default && prepareAction.Action != default)
                        {
                            if (!sExecutingActions.TryAdd(prepareAction.Action, prepareAction))
                            {
                                sExecutingActions[prepareAction.Action] = prepareAction;
                            }
                        }
                    }
                    sPrepareExecutionActions.Clear();
                }
            }

            if (sExecutingActions.Count == 0) return;

            var dt = Time.deltaTime;
            var unDt = Time.unscaledDeltaTime;

            // 3. 遍历执行队列（复制到缓存数组避免遍历冲突）
            var executingCount = sExecutingActions.Count;
            
            // 扩容缓存数组（如果需要）
            if (sExecutingSnapshot.Length < executingCount)
            {
                sExecutingSnapshot = new IActionController[executingCount * 2];
            }

            sExecutingActions.Values.CopyTo(sExecutingSnapshot, 0);

            for (int i = 0; i < executingCount; i++)
            {
                var execute = sExecutingSnapshot[i];
                if (execute == default) continue;

                var deltaTime = execute.UpdateMode is ActionUpdateModes.ScaledDeltaTime ? dt : unDt;
                
                if (UpdateAction(execute, deltaTime))
                {
                    sToActionRemove.Add(execute);
                }
            }

            // 清空快照引用（避免持有已回收对象）
            Array.Clear(sExecutingSnapshot, 0, executingCount);

            // 4. 将完成的队列移出并加入待回收队列
            if (sToActionRemove.Count > 0)
            {
                for (int i = 0; i < sToActionRemove.Count; i++)
                {
                    var controller = sToActionRemove[i];
                    if (controller != default && controller.Action != default)
                    {
                        sExecutingActions.Remove(controller.Action);
                        sPendingRecycleControllers.Add(controller);
                    }
                }
                sToActionRemove.Clear();
            }
        }

        private static void ProcessRecycle()
        {
            // 先回收 Action（触发 OnDeinit，加入回收队列）
            ActionRecyclerManager.ProcessAll();
            
            // 再回收 Controller（确保 Action 已完全回收）
            if (sPendingRecycleControllers.Count > 0)
            {
                for (int i = 0; i < sPendingRecycleControllers.Count; i++)
                {
                    try
                    {
                        var controller = sPendingRecycleControllers[i];
                        if (controller != default)
                        {
                            controller.Recycle();
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[ActionKit] 回收 Controller 异常: {e.Message}");
                    }
                }
                sPendingRecycleControllers.Clear();
            }
        }

        #endregion

        #region 辅助方法

        private static bool UpdateAction(IActionController controller, float dt)
        {
            if (controller == default || controller.Action == default) return true;

            // 检查是否已取消
            if (controller.IsCancelled)
            {
                try
                {
                    controller.OnEnd();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ActionKit] 取消 Action OnEnd 异常: {e.Message}");
                }
                return true;
            }

            try
            {
                if (controller.Action.Deinited || controller.Action.Update(dt))
                {
                    controller.Finish?.Invoke(controller);
                    controller.OnEnd();
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ActionKit] 更新 Action 异常: {e.Message}\n{controller.Action.GetDebugInfo()}");
                controller.OnEnd();
                return true;
            }

            return false;
        }

        private static bool InsertSystem<T>(
            ref PlayerLoopSystem playerLoop,
            Type systemType,
            PlayerLoopSystem.UpdateFunction updateFunction,
            Type insertAfter)
        {
            if (playerLoop.type != typeof(T))
            {
                if (playerLoop.subSystemList == null) return false;

                for (int i = 0; i < playerLoop.subSystemList.Length; i++)
                {
                    if (InsertSystem<T>(ref playerLoop.subSystemList[i], systemType, updateFunction, insertAfter))
                    {
                        return true;
                    }
                }
                return false;
            }

            var subsystems = new List<PlayerLoopSystem>(playerLoop.subSystemList ?? Array.Empty<PlayerLoopSystem>());

            // 检查是否已存在
            if (subsystems.Exists(s => s.type == systemType))
            {
                return true;
            }

            var newSystem = new PlayerLoopSystem
            {
                type = systemType,
                updateDelegate = updateFunction
            };

            if (insertAfter == null)
            {
                subsystems.Insert(0, newSystem);
            }
            else
            {
                var index = subsystems.FindIndex(s => s.type == insertAfter);
                if (index >= 0)
                {
                    subsystems.Insert(index + 1, newSystem);
                }
                else
                {
                    subsystems.Add(newSystem);
                }
            }

            playerLoop.subSystemList = subsystems.ToArray();
            return true;
        }

#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                Cleanup();
            }
            else if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                // 编辑器模式下重置初始化标记，允许重新注册
                sInitialized = false;
            }
        }

        private static void Cleanup()
        {
            lock (sLock)
            {
                // 强制回收所有未完成的 Action
                foreach (var kvp in sExecutingActions)
                {
                    try
                    {
                        if (kvp.Value != default && kvp.Value.Action != default)
                        {
                            kvp.Value.Action.OnDeinit();
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[ActionKit] 清理 Action 异常: {e.Message}");
                    }
                }

                sPrepareExecutionActions.Clear();
                sExecutingActions.Clear();
                sToActionRemove.Clear();
                sPendingRecycleControllers.Clear();
                sPendingCancelControllers.Clear();
                sPendingCancelSet.Clear();
                
                // 清理回收管理器
                ActionRecyclerManager.EditorCleanupAll();
            }
        }

        /// <summary>
        /// [编辑器专用] 获取当前执行中的 Action 数量
        /// </summary>
        public static int ExecutingCount => sExecutingActions.Count;

        /// <summary>
        /// [编辑器专用] 获取所有执行中的 Action
        /// </summary>
        public static void GetExecutingActions(List<IAction> result)
        {
            result.Clear();
            // 使用 for 循环避免枚举器分配
            var enumerator = sExecutingActions.GetEnumerator();
            while (enumerator.MoveNext())
            {
                result.Add(enumerator.Current.Key);
            }
            enumerator.Dispose();
        }

        /// <summary>
        /// [编辑器专用] 获取执行器实例（兼容旧监控代码）
        /// </summary>
        public static IActionExecutor GetExecutorInstance()
        {
            return new PlayerLoopExecutorAdapter();
        }

        /// <summary>
        /// PlayerLoop 执行器适配器（用于编辑器监控）
        /// </summary>
        private class PlayerLoopExecutorAdapter : IActionExecutor
        {
            public void Execute(IActionController controller)
            {
                ActionKitPlayerLoopSystem.Execute(controller);
            }

            public int ExecutingCount => ActionKitPlayerLoopSystem.ExecutingCount;

            public void GetExecutingActions(List<IAction> result)
            {
                ActionKitPlayerLoopSystem.GetExecutingActions(result);
            }
        }
#endif

        #endregion
    }
}
