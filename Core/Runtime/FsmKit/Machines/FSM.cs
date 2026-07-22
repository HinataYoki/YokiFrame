using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 提供同一时间只运行一个状态的泛型有限状态机。
    /// </summary>
    /// <typeparam name="TEnum">状态枚举类型。</typeparam>
    public partial class FSM<TEnum> : IFSM<TEnum> where TEnum : Enum
    {
        /// <summary>当前或最近选择的状态，供派生状态机读取和受控修改。</summary>
        public IState CurState { get; protected set; }

        /// <summary>当前或最近选择的状态枚举值。</summary>
        public TEnum CurEnum { get; protected set; }

        /// <summary>状态机生命周期阶段。</summary>
        public MachineState MachineState => mMachineState;

        /// <summary>派生状态机可访问的生命周期字段。</summary>
        protected MachineState mMachineState = MachineState.End;

        /// <summary>派生状态机可访问的状态字典。</summary>
        protected readonly Dictionary<TEnum, IState> mStateDic;

        private bool mIsDisposed;
        private bool mIsTransitioning;

        /// <summary>
        /// 创建空状态机，并按枚举数量预分配状态字典。
        /// </summary>
        /// <param name="name">Editor/Tools 使用的可选诊断名称；Player 不保存。</param>
        public FSM(string name = null)
        {
#if UNITY_EDITOR || (GODOT && TOOLS)
            mName = NormalizeName(name);
#endif
            mStateDic = new(Enum.GetValues(typeof(TEnum)).Length);
#if UNITY_EDITOR || (GODOT && TOOLS)
            FsmKitRegistry.Register(this, Name);
            FsmEditorHook.RaiseFsmCreated(this);
#endif
        }

        /// <summary>获取指定状态。</summary>
        /// <param name="id">状态标识。</param>
        /// <param name="state">找到的状态。</param>
        public void Get(TEnum id, out IState state)
        {
            ThrowIfDisposed();
            mStateDic.TryGetValue(id, out state);
        }

        /// <summary>添加或替换状态；替换当前运行状态时闭合旧生命周期并尝试启动新状态。</summary>
        /// <param name="id">状态标识。</param>
        /// <param name="state">状态实例。</param>
        public void Add(TEnum id, IState state)
        {
            EnsureMutationAllowed();
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (mStateDic.TryGetValue(id, out var previousState))
            {
                if (ReferenceEquals(previousState, state))
                {
                    return;
                }

                ReplaceState(id, previousState, state);
                return;
            }

            mStateDic.Add(id, state);
#if UNITY_EDITOR || (GODOT && TOOLS)
            RecordStateOrder(id);
#endif
            if (CurState == null)
            {
                CurState = state;
                CurEnum = id;
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            PublishStateAdded(id);
#endif
        }

        /// <summary>移除并释放状态；移除当前状态时把机器复位为空 End。</summary>
        /// <param name="id">状态标识。</param>
        public void Remove(TEnum id)
        {
            EnsureMutationAllowed();
            if (!mStateDic.TryGetValue(id, out var state))
            {
                return;
            }

            BeginLifecycleTransition();
            bool isCurrent;
            try
            {
                isCurrent = ReferenceEquals(CurState, state);
                if (isCurrent && mMachineState != MachineState.End)
                {
                    state.End();
                }

                state.Dispose();
                mStateDic.Remove(id);
#if UNITY_EDITOR || (GODOT && TOOLS)
                RemoveStateOrder(id);
#endif
                if (isCurrent)
                {
                    ResetSelection();
                }
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
            PublishStateRemoved(id);
#endif
        }

        /// <summary>在 Running 阶段切换到满足进入条件的不同状态。</summary>
        /// <param name="id">状态标识。</param>
        public void Change(TEnum id)
        {
            EnsureMutationAllowed();
            if (mMachineState != MachineState.Running ||
                !mStateDic.TryGetValue(id, out var state) ||
                ReferenceEquals(state, CurState))
            {
                return;
            }

            TEnum previousId = CurEnum;
            BeginLifecycleTransition();
            try
            {
                if (!state.Condition())
                {
                    return;
                }

                CurState.End();
                CurState = state;
                CurEnum = id;
                state.Start();
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
            PublishStateChanged(previousId, id);
#endif
        }

        /// <summary>在 Running 阶段带参切换，目标不支持参数时回落无参进入。</summary>
        /// <typeparam name="TArgs">进入参数类型。</typeparam>
        /// <param name="id">状态标识。</param>
        /// <param name="args">进入参数。</param>
        public void Change<TArgs>(TEnum id, TArgs args)
        {
            EnsureMutationAllowed();
            if (mMachineState != MachineState.Running ||
                !mStateDic.TryGetValue(id, out var state) ||
                ReferenceEquals(state, CurState))
            {
                return;
            }

            TEnum previousId = CurEnum;
            BeginLifecycleTransition();
            try
            {
                if (!state.Condition())
                {
                    return;
                }

                CurState.End();
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
            PublishStateChanged(previousId, id);
#endif
        }

        /// <summary>结束当前活动状态、释放全部状态并清空选择与诊断记录。</summary>
        public void Clear()
        {
            EnsureMutationAllowed();
            BeginLifecycleTransition();
            try
            {
                ClearStates();
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
            FsmEditorHook.RaiseFsmCleared(this);
#endif
        }

        /// <summary>仅在 Running 阶段向当前状态转发自定义更新。</summary>
        public void CustomUpdate()
        {
            ThrowIfDisposed();
            if (mMachineState == MachineState.Running)
            {
                CurState?.CustomUpdate();
            }
        }

        /// <summary>结束当前活动状态，但保留选择以支持后续无参重启。</summary>
        public void End()
        {
            EnsureMutationAllowed();
            if (mMachineState == MachineState.End)
            {
                return;
            }

            BeginLifecycleTransition();
            try
            {
                CurState?.End();
                mMachineState = MachineState.End;
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
            FsmKitRegistry.NotifyStateChanged(this);
#endif
        }

        /// <summary>仅在 Running 阶段向当前状态转发固定更新。</summary>
        public void FixedUpdate()
        {
            ThrowIfDisposed();
            if (mMachineState == MachineState.Running)
            {
                CurState?.FixedUpdate();
            }
        }

        /// <summary>从当前选择启动；运行中或进入条件失败时保持 no-op。</summary>
        public void Start()
        {
            TryStartState(CurEnum, CurState);
        }

        /// <summary>从指定状态启动；目标缺失、运行中或条件失败时保持 no-op。</summary>
        /// <param name="id">状态标识。</param>
        public void Start(TEnum id)
        {
            EnsureMutationAllowed();
            if (mStateDic.TryGetValue(id, out var state))
            {
                TryStartState(id, state);
            }
        }

        /// <summary>暂停当前运行状态并停止后续 tick 与消息转发。</summary>
        public void Suspend()
        {
            EnsureMutationAllowed();
            if (CurState == null || mMachineState != MachineState.Running)
            {
                return;
            }

            BeginLifecycleTransition();
            try
            {
                CurState.Suspend();
                mMachineState = MachineState.Suspend;
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
            FsmKitRegistry.NotifyStateChanged(this);
#endif
        }

        /// <summary>仅在 Running 阶段向当前状态转发普通更新。</summary>
        public void Update()
        {
            ThrowIfDisposed();
            if (mMachineState == MachineState.Running)
            {
                CurState?.Update();
            }
        }

        /// <summary>仅在 Running 阶段向当前状态转发强类型消息。</summary>
        /// <typeparam name="TMsg">消息类型。</typeparam>
        /// <param name="message">消息值。</param>
        public void SendMessage<TMsg>(TMsg message)
        {
            ThrowIfDisposed();
            if (mMachineState == MachineState.Running)
            {
                CurState?.SendMessage(message);
            }
        }

        /// <summary>发布释放事件、注销稳定实例，再闭合并清空全部状态。</summary>
        void IState.Dispose()
        {
            if (mIsDisposed)
            {
                return;
            }

            EnsureMutationAllowed();
            mIsDisposed = true;
            mIsTransitioning = true;
#if UNITY_EDITOR || (GODOT && TOOLS)
            FsmEditorHook.RaiseFsmDisposed(this);
            FsmKitRegistry.Unregister(this);
#endif
            try
            {
                ClearStates();
            }
            finally
            {
                mIsTransitioning = false;
#if UNITY_EDITOR || (GODOT && TOOLS)
                FsmEditorHook.RaiseFsmCleared(this);
#endif
            }
        }

        /// <summary>替换已有状态，并按当前机器阶段决定新状态是否继续运行。</summary>
        /// <param name="id">被替换的状态标识。</param>
        /// <param name="previousState">即将释放的旧状态。</param>
        /// <param name="replacement">接管标识的新状态。</param>
        private void ReplaceState(TEnum id, IState previousState, IState replacement)
        {
            BeginLifecycleTransition();
            try
            {
                bool isCurrent = ReferenceEquals(CurState, previousState);
                bool shouldRestart = isCurrent && mMachineState == MachineState.Running;
                if (isCurrent && mMachineState != MachineState.End)
                {
                    previousState.End();
                }

                previousState.Dispose();
                mStateDic[id] = replacement;
#if UNITY_EDITOR || (GODOT && TOOLS)
                RecordStateOrder(id);
#endif
                if (isCurrent)
                {
                    CurState = replacement;
                    CurEnum = id;
                    mMachineState = MachineState.End;
                    if (shouldRestart && replacement.Condition())
                    {
                        mMachineState = MachineState.Running;
                        replacement.Start();
                    }
                }
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
            PublishStateRemoved(id);
            PublishStateAdded(id);
#endif
        }

        /// <summary>尝试无参启动指定状态，并在成功后发布诊断记录。</summary>
        /// <param name="id">目标状态标识。</param>
        /// <param name="state">目标状态实例。</param>
        protected void TryStartState(TEnum id, IState state)
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
                state.Start();
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

    }
}
