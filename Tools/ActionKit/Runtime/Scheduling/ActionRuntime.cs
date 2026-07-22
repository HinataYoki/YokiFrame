using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 集中执行 Action 初始化、推进、暂停传播和 exactly-once 释放规则。
    /// </summary>
    internal static class ActionRuntime
    {
        private static readonly ConditionalWeakTable<IAction, ExternalLifecycleState> sExternalLifecycle = new();
        private static readonly ConditionalWeakTable<IAction, ExternalLifecycleState>.CreateValueCallback sCreateExternalState =
            static _ => new ExternalLifecycleState();

        /// <summary>
        /// 确保当前轮次只初始化一次；组合容器负责初始化自己的直接子树。
        /// </summary>
        /// <param name="action">待初始化 Action。</param>
        internal static void EnsureInitialized(IAction action)
        {
            if (action.Deinited) throw new InvalidOperationException("A deinited Action lease cannot be advanced.");
            if (action is ActionBase actionBase)
            {
                if (actionBase.IsInitialized) return;
                action.OnInit();
                actionBase.MarkInitialized();
                return;
            }

            ExternalLifecycleState state = GetExternalState(action);
            if (state.Initialized) return;
            state.FinishInvoked = false;
            action.OnInit();
            state.Initialized = true;
        }

        /// <summary>
        /// 推进一个 Action，并在正常完成时调用一次 OnFinish。
        /// </summary>
        /// <param name="action">待推进 Action。</param>
        /// <param name="deltaTime">本次时间步长。</param>
        /// <returns>当前 Action 正常完成时返回 true。</returns>
        internal static bool Update(IAction action, float deltaTime)
        {
            EnsureInitialized(action);
            if (action.ActionState == ActionStatus.Finished)
                return TryInvokeNormalFinish(action);
            if (action.Paused) return false;

            if (action.ActionState == ActionStatus.NotStart)
            {
                action.OnStart();
                if (action.ActionState != ActionStatus.Finished)
                {
                    action.ActionState = ActionStatus.Started;
                    return false;
                }
            }
            else if (action.ActionState == ActionStatus.Started)
            {
                action.OnExecute(deltaTime);
                if (action.ActionState != ActionStatus.Finished) return false;
            }

            return TryInvokeNormalFinish(action);
        }

        /// <summary>
        /// 为 Repeat 新一轮重新初始化现有子树，不改变父级所有权或运行 ID。
        /// </summary>
        /// <param name="action">待重启子树根。</param>
        internal static void Restart(IAction action)
        {
            if (action.Deinited) throw new InvalidOperationException("A deinited Action lease cannot be restarted.");
            if (action is ActionBase actionBase)
            {
                if (action.ActionID == 0)
                    actionBase.PrepareExecution(ActionKitScheduler.NextActionId());
            }
            else
            {
                if (action.ActionID == 0)
                    throw new InvalidOperationException("A custom IAction child must provide a non-zero ActionID.");
                ExternalLifecycleState state = GetExternalState(action);
                state.Initialized = false;
                state.FinishInvoked = false;
            }
            action.OnInit();
            if (action is ActionBase initializedAction) initializedAction.MarkInitialized();
            else GetExternalState(action).Initialized = true;
        }

        /// <summary>为未继承 ActionBase 的自定义根重置协调器状态，供新的 Start 租约重新初始化。</summary>
        /// <param name="action">具备非零自有 Action ID 的自定义根。</param>
        internal static void PrepareExternalExecution(IAction action)
        {
            ExternalLifecycleState state = GetExternalState(action);
            state.Initialized = false;
            state.FinishInvoked = false;
        }

        /// <summary>在用户 OnInit 前为整棵树补齐非零 ID，供活动唯一性校验使用。</summary>
        /// <param name="action">即将进入 Scheduler 的根动作树。</param>
        internal static void PrepareTreeActionIds(IAction action)
        {
            PrepareTreeActionIds(action, 0);
        }

        /// <summary>
        /// 沿动作树传播暂停或恢复，并调用可选 ActionBase 生命周期钩子。
        /// </summary>
        /// <param name="action">目标动作树根。</param>
        /// <param name="paused">新的暂停状态。</param>
        internal static void SetPaused(IAction action, bool paused)
        {
            if (action == null) return;
            SetPaused(action, paused, 0);
        }

        /// <summary>
        /// 沿动作树传播时间源变化，供外部驱动 Action 同步自身配置。
        /// </summary>
        /// <param name="action">目标动作树根。</param>
        /// <param name="updateMode">新的时间源。</param>
        internal static void SetUpdateMode(IAction action, ActionUpdateModes updateMode)
        {
            if (action == null) return;
            SetUpdateMode(action, updateMode, 0);
        }

        /// <summary>
        /// 深度优先释放完整树；任一 OnDeinit 异常不会阻止其它节点清理。
        /// </summary>
        /// <param name="action">待释放动作树根。</param>
        /// <param name="pendingRecycle">接收内置池化节点的复用队列。</param>
        /// <returns>完整清理期间捕获的首个异常；全部成功时返回 null。</returns>
        internal static Exception DeinitializeTree(IAction action, List<IPooledAction> pendingRecycle)
        {
            if (action == null) return null;
            Exception firstException = null;
            DeinitializeTree(action, pendingRecycle, 0, ref firstException);
            return firstException;
        }

        /// <summary>
        /// 把 fluent 扩展刚创建的节点追加到容器；追加失败时关闭未调度租约，避免池对象和回调泄漏。
        /// </summary>
        /// <param name="container">目标父容器。</param>
        /// <param name="createdAction">当前扩展刚创建且尚未交给其它 owner 的 Action。</param>
        /// <returns>父容器，供 fluent 调用继续装配。</returns>
        internal static ISequence AppendCreated(ISequence container, IAction createdAction)
        {
            try
            {
                return container.Append(createdAction);
            }
            catch
            {
                ActionKitScheduler.DiscardUnscheduled(createdAction);
                throw;
            }
        }

        /// <summary>
        /// 递归传播暂停状态并限制异常深树，避免损坏输入导致栈溢出。
        /// </summary>
        private static void SetPaused(IAction action, bool paused, int depth)
        {
            ValidateDepth(depth);
            if (action.Paused != paused)
            {
                action.Paused = paused;
                if (action is ActionBase actionBase)
                {
                    if (paused) actionBase.OnPause(); else actionBase.OnResume();
                }
            }

            if (!(action is IActionContainerInternal container)) return;
            for (var index = 0; index < container.ChildCount; index++)
                SetPaused(container.GetChild(index), paused, depth + 1);
        }

        /// <summary>
        /// 递归传播 controller 时间源；调用只发生在显式配置变化时，不属于每帧热路径。
        /// </summary>
        private static void SetUpdateMode(IAction action, ActionUpdateModes updateMode, int depth)
        {
            ValidateDepth(depth);
            if (action is ActionBase actionBase) actionBase.OnUpdateModeChanged(updateMode);
            if (!(action is IActionContainerInternal container)) return;
            for (var index = 0; index < container.ChildCount; index++)
                SetUpdateMode(container.GetChild(index), updateMode, depth + 1);
        }

        /// <summary>
        /// 递归释放子节点后释放父节点，确保容器清空列表前所有孩子仍可访问。
        /// </summary>
        private static void DeinitializeTree(
            IAction action,
            List<IPooledAction> pendingRecycle,
            int depth,
            ref Exception firstException)
        {
            ValidateDepth(depth);
            if (action.Deinited) return;

            if (action is IActionContainerInternal container)
            {
                for (var index = 0; index < container.ChildCount; index++)
                {
                    DeinitializeTree(
                        container.GetChild(index),
                        pendingRecycle,
                        depth + 1,
                        ref firstException);
                }
            }

            try { action.OnDeinit(); }
            catch (Exception exception)
            {
                if (firstException == null) firstException = exception;
                ActionKitFailureReporter.TryLog("[ActionKit] OnDeinit failed: ", exception);
            }
            if (action is ActionBase actionBase) actionBase.MarkDeinited();
            else sExternalLifecycle.Remove(action);
            ActionOwnership.Release(action);
            if (action is IPooledAction pooledAction) pendingRecycle.Add(pooledAction);
        }

        /// <summary>调用一次正常完成钩子；标记先于用户代码写入，异常后也不会被手动 Update 重试。</summary>
        private static bool InvokeFinishOnce(IAction action)
        {
            if (action is ActionBase actionBase)
            {
                if (!actionBase.TryBeginFinish()) return true;
            }
            else
            {
                ExternalLifecycleState state = GetExternalState(action);
                if (state.FinishInvoked) return true;
                state.FinishInvoked = true;
            }

            action.OnFinish();
            return true;
        }

        /// <summary>只在当前推进尚未收到取消或控制故障时进入正常完成钩子。</summary>
        /// <param name="action">已经标记完成的 Action。</param>
        /// <returns>已进入或已经执行过正常完成钩子时返回 true。</returns>
        private static bool TryInvokeNormalFinish(IAction action)
        {
            if (ActionKitScheduler.CurrentAdvanceTerminationRequested) return false;
            return InvokeFinishOnce(action);
        }

        /// <summary>获取未继承 ActionBase 的自定义 Action 弱键生命周期状态。</summary>
        private static ExternalLifecycleState GetExternalState(IAction action) =>
            sExternalLifecycle.GetValue(action, sCreateExternalState);

        /// <summary>递归补齐 ActionBase ID，并拒绝未提供 ID 的外部 IAction。</summary>
        private static void PrepareTreeActionIds(IAction action, int depth)
        {
            ValidateDepth(depth);
            if (action is ActionBase actionBase)
            {
                if (action.ActionID == 0)
                    actionBase.PrepareExecution(ActionKitScheduler.NextActionId());
            }
            else if (action.ActionID == 0)
            {
                throw new InvalidOperationException("A custom IAction must provide a non-zero ActionID.");
            }

            if (!(action is IActionContainerInternal container)) return;
            for (var index = 0; index < container.ChildCount; index++)
                PrepareTreeActionIds(container.GetChild(index), depth + 1);
        }

        /// <summary>拒绝超过安全上限的动作树深度。</summary>
        private static void ValidateDepth(int depth)
        {
            if (depth >= ActionTreeLimits.MAX_DEPTH)
                throw new InvalidOperationException("Action tree exceeds the supported lifecycle depth.");
        }

        /// <summary>保存未继承 ActionBase 的自定义 Action 初始化和完成钩子状态。</summary>
        private sealed class ExternalLifecycleState
        {
            internal bool Initialized;
            internal bool FinishInvoked;
        }
    }
}
