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
        public MachineState MachineState => machineState;

        protected MachineState machineState = MachineState.End;
        protected readonly Dictionary<TEnum, IState> mStateDic = new();
        
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
#if UNITY_EDITOR
            Name = name ?? $"FSM<{typeof(TEnum).Name}>";
            if (FsmEditorHook.OnFsmCreated != null)
                FsmEditorHook.OnFsmCreated.Invoke(this);
#endif
        }

#if UNITY_EDITOR
        public IReadOnlyDictionary<int, IState> GetAllStates()
        {
            mStateIdCache ??= new Dictionary<int, IState>();
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

            if (CurState == null)
            {
                CurState = state;
                CurEnum = id;
            }
#if UNITY_EDITOR
            if (FsmEditorHook.OnStateAdded != null)
                FsmEditorHook.OnStateAdded.Invoke(this, id.ToString());
#endif
        }

        public void Remove(TEnum id)
        {
            if (mStateDic.TryGetValue(id, out var state))
            {
                if (CurState == state && machineState is MachineState.Running)
                {
                    CurState.End(); 
                    CurState = null;
                    machineState = MachineState.End;
                }
                mStateDic[id].Dispose();
                mStateDic.Remove(id);
#if UNITY_EDITOR
                if (FsmEditorHook.OnStateRemoved != null)
                    FsmEditorHook.OnStateRemoved.Invoke(this, id.ToString());
#endif
            }
        }

        public void Change(TEnum id)
        {
            if (machineState is not MachineState.Running) return;
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
                    if (FsmEditorHook.OnStateChanged != null)
                        FsmEditorHook.OnStateChanged.Invoke(this, prevEnum.ToString(), id.ToString());
#endif
                }
            }
        }

        public void Change<TArgs>(TEnum id, TArgs args)
        {
            if (machineState is not MachineState.Running) return;
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
                    if (FsmEditorHook.OnStateChanged != null)
                        FsmEditorHook.OnStateChanged.Invoke(this, prevEnum.ToString(), id.ToString());
#endif
                }
            }
        }

        public void Clear()
        {
            if (MachineState is not MachineState.End && CurState != null)
            {
                End();
                CurState = null;
            }
            foreach (var state in mStateDic.Values)
            {
                state.Dispose();
            }
            mStateDic.Clear();
            machineState = MachineState.End;
#if UNITY_EDITOR
            FsmEditorHook.OnFsmCleared?.Invoke(this);
#endif
        }

        public void CustomUpdate()
        {
            if (machineState is MachineState.Running)
            {
                CurState?.CustomUpdate();
            }
        }

        public void End()
        {
            if (machineState is not MachineState.End)
            {
                machineState = MachineState.End;
                CurState?.End();
            }
        }

        public void FixedUpdate()
        {
            if (machineState is MachineState.Running)
            {
                CurState?.FixedUpdate();
            }
        }

        public void Start()
        {
            if (CurState != null && machineState is not MachineState.Running)
            {
                machineState = MachineState.Running;
                CurState.Start();
#if UNITY_EDITOR
                if (FsmEditorHook.OnFsmStarted != null)
                    FsmEditorHook.OnFsmStarted.Invoke(this, CurEnum.ToString());
#endif
            }
        }

        public void Start(TEnum id)
        {
            if (mStateDic.TryGetValue(id, out var state) && machineState is not MachineState.Running)
            {
                machineState = MachineState.Running;
                CurState = state;
                CurEnum = id;
                state.Start();
#if UNITY_EDITOR
                if (FsmEditorHook.OnFsmStarted != null)
                    FsmEditorHook.OnFsmStarted.Invoke(this, id.ToString());
#endif
            }
        }

        public void Suspend()
        {
            if (CurState != null && machineState is MachineState.Running)
            {
                machineState = MachineState.Suspend;
                CurState?.Suspend();
            }
        }

        public void Update()
        {
            if (machineState is MachineState.Running)
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
            if (CurState != null && machineState is not MachineState.Running)
            {
                machineState = MachineState.Running;
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
            if (mStateDic.TryGetValue(id, out var state))
            {
                machineState = MachineState.Running;
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
