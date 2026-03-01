using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public class FSM<TEnum> : IFSM<TEnum> where TEnum : Enum
    {
        /// <summary>
        /// 当前状态
        /// </summary>
        public IState CurState { get; protected set; }
        /// <summary>
        /// 当前枚举
        /// </summary>
        public TEnum CurEnum { get; protected set; }
        /// <summary>
        /// 状态机状态
        /// </summary>
        public MachineState MachineState => mMachineState;

        protected MachineState mMachineState = MachineState.End;
        protected readonly Dictionary<TEnum, IState> mStateDic;
        
#if UNITY_EDITOR
        /// <summary>
        /// 状态机名称（用于调试）
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 枚举类型
        /// </summary>
        public Type EnumType => typeof(TEnum);
        // IFSM 接口实现
        IState IFSM.CurrentState => CurState;
        int IFSM.CurrentStateId => CurEnum != null ? Convert.ToInt32(CurEnum) : -1;
        // 用于编辑器可视化的缓存字典
        private Dictionary<int, IState> mStateIdCache;
#endif

        public FSM(string name = null)
        {
            // 根据枚举成员数量预估字典容量
            int enumCount = Enum.GetValues(typeof(TEnum)).Length;
            mStateDic = new Dictionary<TEnum, IState>(enumCount);
            
#if UNITY_EDITOR
            Name = name ?? $"FSM<{typeof(TEnum).Name}>";
            FsmEditorHook.OnFsmCreated?.Invoke(this);
#endif
        }

#if UNITY_EDITOR
        public IReadOnlyDictionary<int, IState> GetAllStates()
        {
            mStateIdCache ??= new();
            mStateIdCache.Clear();
            foreach (var kvp in mStateDic)
                mStateIdCache[Convert.ToInt32(kvp.Key)] = kvp.Value;
            return mStateIdCache;
        }
#endif

        public void Get(TEnum id, out IState state)
        {
            mStateDic.TryGetValue(id, out state);
        }

        public void Add(TEnum id, IState state)
        {
            if (mStateDic.ContainsKey(id))
            {
                if (mStateDic[id] == state) return;
                mStateDic[id].Dispose();
                mStateDic.Remove(id);
            }
            mStateDic.Add(id, state);

            if (CurState is null)
            {
                CurState = state;
                CurEnum = id;
            }
#if UNITY_EDITOR
            FsmEditorHook.OnStateAdded?.Invoke(this, id.ToString());
#endif
        }

        public void Remove(TEnum id)
        {
            if (mStateDic.TryGetValue(id, out var state))
            {
                if (CurState == state && mMachineState is MachineState.Running)
                {
                    CurState.End(); 
                    CurState = null;
                    mMachineState = MachineState.End;
                }
                mStateDic[id].Dispose();
                mStateDic.Remove(id);
#if UNITY_EDITOR
                FsmEditorHook.OnStateRemoved?.Invoke(this, id.ToString());
#endif
            }
        }

        public void Change(TEnum id)
        {
            if (mMachineState is not MachineState.Running) return;
            if (mStateDic.TryGetValue(id, out var state))
            {
                if (state != CurState && state.Condition())
                {
                    var prevEnum = CurEnum;
                    CurState?.End();
                    CurState = state;
                    CurEnum = id;
                    state.Start();
#if UNITY_EDITOR
                    FsmEditorHook.OnStateChanged?.Invoke(this, prevEnum.ToString(), id.ToString());
#endif
                }
            }
        }

        public void Change<TArgs>(TEnum id, TArgs args)
        {
            if (mMachineState is not MachineState.Running) return;
            if (mStateDic.TryGetValue(id, out var state))
            {
                if (state != CurState && state.Condition())
                {
                    var prevEnum = CurEnum;
                    CurState?.End();
                    CurState = state;
                    CurEnum = id;
                    if (state is IState<TArgs> stateWithArgs)
                    {
                        stateWithArgs.Start(args);
                    }
                    else
                    {
                        state.Start();
                    }
#if UNITY_EDITOR
                    FsmEditorHook.OnStateChanged?.Invoke(this, prevEnum.ToString(), id.ToString());
#endif
                }
            }
        }

        public void Clear()
        {
            if (MachineState is not MachineState.End && CurState is not null)
            {
                End();
                CurState = null;
            }
            foreach (var state in mStateDic.Values)
            {
                state.Dispose();
            }
            mStateDic.Clear();
            mMachineState = MachineState.End;
#if UNITY_EDITOR
            FsmEditorHook.OnFsmCleared?.Invoke(this);
#endif
        }

        public void CustomUpdate()
        {
            if (mMachineState is MachineState.Running)
            {
                CurState?.CustomUpdate();
            }
        }

        public void End()
        {
            if (mMachineState is not MachineState.End)
            {
                mMachineState = MachineState.End;
                CurState?.End();
            }
        }

        public void FixedUpdate()
        {
            if (mMachineState is MachineState.Running)
            {
                CurState?.FixedUpdate();
            }
        }

        public void Start()
        {
            if (CurState is not null && mMachineState is not MachineState.Running)
            {
                mMachineState = MachineState.Running;
                CurState.Start();
#if UNITY_EDITOR
                FsmEditorHook.OnFsmStarted?.Invoke(this, CurEnum.ToString());
#endif
            }
        }

        public void Start(TEnum id)
        {
            if (mStateDic.TryGetValue(id, out var state) && mMachineState is not MachineState.Running)
            {
                mMachineState = MachineState.Running;
                CurState = state;
                CurEnum = id;
                state.Start();
#if UNITY_EDITOR
                FsmEditorHook.OnFsmStarted?.Invoke(this, id.ToString());
#endif
            }
        }

        public void Suspend()
        {
            if (CurState is not null && mMachineState is MachineState.Running)
            {
                mMachineState = MachineState.Suspend;
                CurState?.Suspend();
            }
        }

        public void Update()
        {
            if (mMachineState is MachineState.Running)
            {
                CurState?.Update();
            }
        }

        public void SendMessage<TMsg>(TMsg message)
        {
            if (MachineState is MachineState.Running)
            {
                CurState?.SendMessage(message);
            }
        }

        void IState.Dispose()
        {
#if UNITY_EDITOR
            FsmEditorHook.OnFsmDisposed?.Invoke(this);
#endif
            Clear();
        }
    }

    public class FSM<TEnum, TArgs> : FSM<TEnum>, IFSM<TEnum, TArgs> where TEnum : Enum
    {
        public void Start(TArgs args)
        {
            if (CurState is not null && mMachineState is not MachineState.Running)
            {
                mMachineState = MachineState.Running;
                if (CurState is IState<TArgs> stateWithArgs)
                {
                    stateWithArgs.Start(args);
                }
                else
                {
                    CurState.Start();
                }
            }
        }

        public void Start(TEnum id, TArgs args)
        {
            if (mStateDic.TryGetValue(id, out var state) && mMachineState is not MachineState.Running)
            {
                mMachineState = MachineState.Running;
                CurState = state;
                CurEnum = id;
                if (state is IState<TArgs> stateWithArgs)
                {
                    stateWithArgs.Start(args);
                }
                else
                {
                    state.Start();
                }
            }
        }
    }
}
