using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public class FSM<TEnum> : IFSM<TEnum> where TEnum : Enum
    {
        public IState CurState { get; protected set; }
        public TEnum CurEnum { get; protected set; }
        public MachineState MachineState => mMachineState;

        protected MachineState mMachineState = MachineState.End;
        protected readonly Dictionary<TEnum, IState> mStateDic;

#if UNITY_EDITOR || GODOT
        /// <summary>状态机名称（用于调试）</summary>
        public string Name { get; set; }
        /// <summary>枚举类型</summary>
        public Type EnumType => typeof(TEnum);
        IState IFSM.CurrentState => CurState;
        int IFSM.CurrentStateId => CurEnum != null ? Convert.ToInt32(CurEnum) : -1;
        private Dictionary<int, IState> mStateIdCache;
        private readonly Dictionary<TEnum, int> mStateOrderDic = new();
        private int mNextStateOrderIndex;
#endif

        public FSM(string name = null)
        {
            int enumCount = Enum.GetValues(typeof(TEnum)).Length;
            mStateDic = new Dictionary<TEnum, IState>(enumCount);

#if UNITY_EDITOR || GODOT
            Name = name ?? $"FSM<{typeof(TEnum).Name}>";
            FsmEditorHook.OnFsmCreated?.Invoke(this);
#endif
        }

#if UNITY_EDITOR || GODOT
        public IReadOnlyDictionary<int, IState> GetAllStates()
        {
            mStateIdCache ??= new();
            mStateIdCache.Clear();
            foreach (var kvp in mStateDic)
                mStateIdCache[Convert.ToInt32(kvp.Key)] = kvp.Value;
            return new Dictionary<int, IState>(mStateIdCache);
        }

        public int GetStateOrderIndex(int stateId)
        {
            var id = (TEnum)Enum.ToObject(typeof(TEnum), stateId);
            return mStateOrderDic.TryGetValue(id, out var orderIndex) ? orderIndex : stateId;
        }
#endif

        public void Get(TEnum id, out IState state) => mStateDic.TryGetValue(id, out state);

        public void Add(TEnum id, IState state)
        {
            var replacedCurrent = false;
            if (mStateDic.ContainsKey(id))
            {
                if (mStateDic[id] == state) return;
                replacedCurrent = CurState == mStateDic[id];
#if UNITY_EDITOR || GODOT
                FsmEditorHook.OnStateRemoved?.Invoke(this, id.ToString());
#endif
                mStateDic[id].Dispose();
                mStateDic.Remove(id);
            }
            mStateDic.Add(id, state);
#if UNITY_EDITOR || GODOT
            if (!mStateOrderDic.ContainsKey(id))
                mStateOrderDic[id] = mNextStateOrderIndex++;
#endif

            if (CurState is null)
            {
                CurState = state;
                CurEnum = id;
            }
            else if (replacedCurrent)
            {
                CurState = state;
                CurEnum = id;
            }
#if UNITY_EDITOR || GODOT
            FsmEditorHook.OnStateAdded?.Invoke(this, id.ToString());
#endif
        }

        public void Remove(TEnum id)
        {
            if (mStateDic.TryGetValue(id, out var state))
            {
                var isCurrentState = CurState == state;
                if (isCurrentState && mMachineState is MachineState.Running)
                {
                    CurState.End();
                }
                mStateDic[id].Dispose();
                mStateDic.Remove(id);
                if (isCurrentState)
                {
                    CurState = null;
                    CurEnum = default;
                    mMachineState = MachineState.End;
                }
#if UNITY_EDITOR || GODOT
                mStateOrderDic.Remove(id);
#endif
#if UNITY_EDITOR || GODOT
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
#if UNITY_EDITOR || GODOT
                    var prevEnum = CurEnum;
#endif
                    CurState?.End();
                    CurState = state;
                    CurEnum = id;
                    state.Start();
#if UNITY_EDITOR || GODOT
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
#if UNITY_EDITOR || GODOT
                    var prevEnum = CurEnum;
#endif
                    CurState?.End();
                    CurState = state;
                    CurEnum = id;
                    if (state is IState<TArgs> stateWithArgs)
                        stateWithArgs.Start(args);
                    else
                        state.Start();
#if UNITY_EDITOR || GODOT
                    FsmEditorHook.OnStateChanged?.Invoke(this, prevEnum.ToString(), id.ToString());
#endif
                }
            }
        }

        public void Clear()
        {
            if (mMachineState is not MachineState.End && CurState is not null)
            {
                End();
            }
            CurState = null;
            CurEnum = default;
            foreach (var state in mStateDic.Values)
                state.Dispose();
            mStateDic.Clear();
#if UNITY_EDITOR || GODOT
            mStateOrderDic.Clear();
            mNextStateOrderIndex = 0;
#endif
            mMachineState = MachineState.End;
#if UNITY_EDITOR || GODOT
            FsmEditorHook.OnFsmCleared?.Invoke(this);
#endif
        }

        public void CustomUpdate()
        {
            if (mMachineState is MachineState.Running)
                CurState?.CustomUpdate();
        }

        public void End()
        {
            if (mMachineState is not MachineState.End)
            {
                mMachineState = MachineState.End;
                CurState?.End();
                CurEnum = default;
            }
        }

        public void FixedUpdate()
        {
            if (mMachineState is MachineState.Running)
                CurState?.FixedUpdate();
        }

        public void Start()
        {
            if (CurState is not null && mMachineState is not MachineState.Running && CurState.Condition())
            {
                mMachineState = MachineState.Running;
                CurState.Start();
#if UNITY_EDITOR || GODOT
                FsmEditorHook.OnFsmStarted?.Invoke(this, CurEnum.ToString());
#endif
            }
        }

        public void Start(TEnum id)
        {
            if (mStateDic.TryGetValue(id, out var state) && mMachineState is not MachineState.Running && state.Condition())
            {
                mMachineState = MachineState.Running;
                CurState = state;
                CurEnum = id;
                state.Start();
#if UNITY_EDITOR || GODOT
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
                CurState?.Update();
        }

        public void SendMessage<TMsg>(TMsg message)
        {
            if (MachineState is MachineState.Running)
                CurState?.SendMessage(message);
        }

        void IState.Dispose()
        {
#if UNITY_EDITOR || GODOT
            FsmEditorHook.OnFsmDisposed?.Invoke(this);
#endif
            Clear();
        }
    }

    public class FSM<TEnum, TArgs> : FSM<TEnum>, IFSM<TEnum, TArgs> where TEnum : Enum
    {
        public void Start(TArgs args)
        {
            if (CurState is not null && mMachineState is not MachineState.Running && CurState.Condition())
            {
                mMachineState = MachineState.Running;
                if (CurState is IState<TArgs> stateWithArgs)
                    stateWithArgs.Start(args);
                else
                    CurState.Start();
#if UNITY_EDITOR || GODOT
                FsmEditorHook.OnFsmStarted?.Invoke(this, CurEnum.ToString());
#endif
            }
        }

        public void Start(TEnum id, TArgs args)
        {
            if (mStateDic.TryGetValue(id, out var state) && mMachineState is not MachineState.Running && state.Condition())
            {
                mMachineState = MachineState.Running;
                CurState = state;
                CurEnum = id;
                if (state is IState<TArgs> stateWithArgs)
                    stateWithArgs.Start(args);
                else
                    state.Start();
#if UNITY_EDITOR || GODOT
                FsmEditorHook.OnFsmStarted?.Invoke(this, id.ToString());
#endif
            }
        }
    }
}
