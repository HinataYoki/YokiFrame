using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>收纳普通 FSM 的带参启动、释放终态和生命周期重入守卫。</summary>
    public partial class FSM<TEnum> where TEnum : Enum
    {
        /// <summary>尝试带参启动指定状态，不支持该参数类型时回落无参进入。</summary>
        /// <typeparam name="TArgs">进入参数类型。</typeparam>
        /// <param name="id">目标状态标识。</param>
        /// <param name="state">目标状态实例。</param>
        /// <param name="args">进入参数。</param>
        protected void TryStartState<TArgs>(TEnum id, IState state, TArgs args)
        {
            EnsureMutationAllowed();
            if (state == null || mMachineState == MachineState.Running)
            {
                return;
            }

            BeginLifecycleTransition();
            try
            {
                if (!state.Condition())
                {
                    return;
                }

                mMachineState = MachineState.Running;
                CurState = state;
                CurEnum = id;
                StartState(state, args);
            }
            catch
            {
                mMachineState = MachineState.End;
                throw;
            }
            finally
            {
                EndLifecycleTransition();
            }
#if UNITY_EDITOR || (GODOT && TOOLS)
            PublishFsmStarted(id);
#endif
        }

        /// <summary>按参数契约进入状态，不支持参数时保持无参回落。</summary>
        /// <typeparam name="TArgs">进入参数类型。</typeparam>
        /// <param name="state">目标状态。</param>
        /// <param name="args">进入参数。</param>
        protected static void StartState<TArgs>(IState state, TArgs args)
        {
            if (state is IState<TArgs> stateWithArgs)
            {
                stateWithArgs.Start(args);
                return;
            }

            state.Start();
        }

        /// <summary>清空当前选择并把机器复位为 End。</summary>
        private void ResetSelection()
        {
            CurState = null;
            CurEnum = default(TEnum);
            mMachineState = MachineState.End;
        }

        /// <summary>尽力结束并释放全部状态，最终复位选择；失败会在完成结构清理后聚合抛出。</summary>
        private void ClearStates()
        {
            List<Exception> errors = null;
            try
            {
                TryEndCurrentState(ref errors);
                DisposeStates(ref errors);
            }
            finally
            {
                mStateDic.Clear();
#if UNITY_EDITOR || (GODOT && TOOLS)
                ClearStateOrder();
#endif
                ResetSelection();
#if UNITY_EDITOR || (GODOT && TOOLS)
                FsmKitRegistry.ClearRecords(this);
#endif
            }

            if (errors != null)
            {
                throw new AggregateException("One or more FSM state cleanup operations failed.", errors);
            }
        }

        /// <summary>结束当前活动状态并收集异常，使后续状态仍能进入释放流程。</summary>
        /// <param name="errors">按发生顺序收集的清理异常。</param>
        private void TryEndCurrentState(ref List<Exception> errors)
        {
            if (CurState == null || mMachineState == MachineState.End)
            {
                return;
            }

            try
            {
                CurState.End();
            }
            catch (Exception exception)
            {
                AddCleanupError(ref errors, exception);
            }
        }

        /// <summary>逐个释放全部状态并隔离单个状态异常，保证后续状态仍被释放。</summary>
        /// <param name="errors">按发生顺序收集的清理异常。</param>
        private void DisposeStates(ref List<Exception> errors)
        {
            foreach (var state in mStateDic.Values)
            {
                try
                {
                    state.Dispose();
                }
                catch (Exception exception)
                {
                    AddCleanupError(ref errors, exception);
                }
            }
        }

        /// <summary>按需创建异常集合并保留原始异常，避免丢失业务状态的失败上下文。</summary>
        /// <param name="errors">清理异常集合。</param>
        /// <param name="exception">本次捕获的状态异常。</param>
        private static void AddCleanupError(ref List<Exception> errors, Exception exception)
        {
            errors ??= new List<Exception>();
            errors.Add(exception);
        }

        /// <summary>拒绝访问已经完成释放的状态机，防止诊断实例被重新注册。</summary>
        private void ThrowIfDisposed()
        {
            if (mIsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        /// <summary>拒绝释放后复用或在生命周期回调中发起嵌套状态变更。</summary>
        protected void EnsureMutationAllowed()
        {
            ThrowIfDisposed();
            if (mIsTransitioning)
            {
                throw new InvalidOperationException("状态生命周期回调执行期间不能再次修改 FSM。");
            }
        }

        /// <summary>进入生命周期变更区间，使 Start、End、Suspend 和 Dispose 回调不可重入。</summary>
        private void BeginLifecycleTransition()
        {
            EnsureMutationAllowed();
            mIsTransitioning = true;
        }

        /// <summary>离开生命周期变更区间，恢复普通 Update 回调发起状态切换的能力。</summary>
        private void EndLifecycleTransition()
        {
            mIsTransitioning = false;
        }
    }

    /// <summary>提供状态机自身带启动参数的泛型有限状态机。</summary>
    /// <typeparam name="TEnum">状态枚举类型。</typeparam>
    /// <typeparam name="TArgs">启动参数类型。</typeparam>
    public class FSM<TEnum, TArgs> : FSM<TEnum>, IFSM<TEnum, TArgs> where TEnum : Enum
    {
        /// <summary>创建空的带参状态机。</summary>
        /// <param name="name">Editor/Tools 使用的可选诊断名称；Player 不保存。</param>
        public FSM(string name = null) : base(name)
        {
        }

        /// <summary>使用参数从当前选择启动；条件失败或运行中时保持 no-op。</summary>
        /// <param name="args">启动参数。</param>
        public void Start(TArgs args) => TryStartState(CurEnum, CurState, args);

        /// <summary>使用参数从指定状态启动；不支持参数时回落无参进入。</summary>
        /// <param name="id">状态标识。</param>
        /// <param name="args">启动参数。</param>
        public void Start(TEnum id, TArgs args)
        {
            EnsureMutationAllowed();
            if (mStateDic.TryGetValue(id, out var state))
            {
                TryStartState(id, state, args);
            }
        }
    }
}
