using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 层级状态机，可包含多个独立运行的子状态机。
    /// </summary>
    public class HierarchicalSM<TEnum> : IFSM<TEnum> where TEnum : Enum
    {
        private readonly SortedDictionary<TEnum, (IState State, MachineState Status)> mStateDic = new();
        private readonly List<TEnum> mTempKeys = new(8);

        private MachineState mMachineState = MachineState.End;
        public MachineState MachineState => mMachineState;
        public TEnum CurEnum { get; private set; }

#if UNITY_EDITOR || GODOT
        private Dictionary<int, IState> mStateIdCache;
        public string Name { get; set; }
        public Type EnumType => typeof(TEnum);
        IState IFSM.CurrentState => mStateDic.TryGetValue(CurEnum, out var s) ? s.State : null;
        int IFSM.CurrentStateId => CurEnum != null ? Convert.ToInt32(CurEnum) : -1;
        private readonly Dictionary<TEnum, int> mStateOrderDic = new();
        private int mNextStateOrderIndex;

        public int GetStateOrderIndex(int stateId)
        {
            var id = (TEnum)Enum.ToObject(typeof(TEnum), stateId);
            return mStateOrderDic.TryGetValue(id, out var orderIndex) ? orderIndex : stateId;
        }
#endif

        public HierarchicalSM(string name = null)
        {
#if UNITY_EDITOR || GODOT
            Name = name ?? $"HierarchicalSM<{typeof(TEnum).Name}>";
            FsmEditorHook.OnFsmCreated?.Invoke(this);
#endif
        }

#if UNITY_EDITOR || GODOT
        public IReadOnlyDictionary<int, IState> GetAllStates()
        {
            mStateIdCache ??= new();
            mStateIdCache.Clear();
            foreach (var kvp in mStateDic)
                mStateIdCache[Convert.ToInt32(kvp.Key)] = kvp.Value.State;
            return new Dictionary<int, IState>(mStateIdCache);
        }
#endif

        public void Get(TEnum id, out IState state)
        {
            mStateDic.TryGetValue(id, out var statePair);
            state = statePair.State;
        }

        public void Add(TEnum id, IState state)
        {
            if (mStateDic.TryGetValue(id, out var statePair))
            {
                if (statePair.Status is not MachineState.End)
                    statePair.State.End();
                statePair.State.Dispose();
                mStateDic.Remove(id);
#if UNITY_EDITOR || GODOT
                FsmEditorHook.OnStateRemoved?.Invoke(this, id.ToString());
#endif
            }
            switch (state)
            {
                case IFSM fsm:
                    mStateDic.Add(id, (fsm, fsm.MachineState));
                    break;
                default:
                    mStateDic.Add(id, (state, MachineState.End));
                    break;
            }
#if UNITY_EDITOR || GODOT
            if (!mStateOrderDic.ContainsKey(id))
                mStateOrderDic[id] = mNextStateOrderIndex++;
            if (mStateDic.Count == 1)
                CurEnum = id;
            FsmEditorHook.OnStateAdded?.Invoke(this, id.ToString());
#endif
        }

        public void Clear()
        {
            foreach (var state in mStateDic.Values)
            {
                if (state.Status is not MachineState.End)
                    state.State.End();
                state.State.Dispose();
            }
            mStateDic.Clear();
#if UNITY_EDITOR || GODOT
            mStateOrderDic.Clear();
            mNextStateOrderIndex = 0;
            FsmEditorHook.OnFsmCleared?.Invoke(this);
#endif
        }

        public void CustomUpdate()
        {
            if (mMachineState is MachineState.Running)
            {
                foreach (var state in mStateDic.Values)
                {
                    if (state.Status is MachineState.Running)
                        state.State.CustomUpdate();
                }
            }
        }

        public void End()
        {
            mMachineState = MachineState.End;
            mTempKeys.Clear();
            foreach (var kvp in mStateDic)
            {
                if (kvp.Value.Status is not MachineState.End)
                {
                    kvp.Value.State.End();
                    mTempKeys.Add(kvp.Key);
                }
            }
            for (int i = 0; i < mTempKeys.Count; i++)
            {
                var key = mTempKeys[i];
                var entry = mStateDic[key];
                mStateDic[key] = (entry.State, MachineState.End);
            }
        }

        public void FixedUpdate()
        {
            if (mMachineState is MachineState.Running)
            {
                foreach (var state in mStateDic.Values)
                {
                    if (state.Status is MachineState.Running)
                        state.State.FixedUpdate();
                }
            }
        }

        public void Remove(TEnum id)
        {
            if (mStateDic.TryGetValue(id, out var state))
            {
                if (state.Status is not MachineState.End)
                    state.State.End();
                state.State.Dispose();
                mStateDic.Remove(id);
#if UNITY_EDITOR || GODOT
                mStateOrderDic.Remove(id);
                FsmEditorHook.OnStateRemoved?.Invoke(this, id.ToString());
#endif
            }
        }

        public void Start()
        {
            mMachineState = MachineState.Running;
            mTempKeys.Clear();
            foreach (var kvp in mStateDic)
            {
                if (kvp.Value.Status is not MachineState.Running)
                {
                    kvp.Value.State.Start();
                    mTempKeys.Add(kvp.Key);
                }
            }
            for (int i = 0; i < mTempKeys.Count; i++)
            {
                var key = mTempKeys[i];
                var entry = mStateDic[key];
                mStateDic[key] = (entry.State, MachineState.Running);
            }
        }

        public void Suspend()
        {
            if (mMachineState is MachineState.Running)
            {
                mMachineState = MachineState.Suspend;
                mTempKeys.Clear();
                foreach (var kvp in mStateDic)
                {
                    if (kvp.Value.Status is MachineState.Running)
                    {
                        kvp.Value.State.Suspend();
                        mTempKeys.Add(kvp.Key);
                    }
                }
                for (int i = 0; i < mTempKeys.Count; i++)
                {
                    var key = mTempKeys[i];
                    var entry = mStateDic[key];
                    mStateDic[key] = (entry.State, MachineState.Suspend);
                }
            }
        }

        public void Update()
        {
            if (mMachineState is MachineState.Running)
            {
                foreach (var state in mStateDic.Values)
                {
                    if (state.Status is MachineState.Running)
                        state.State.Update();
                }
            }
        }

        public void Change(TEnum id, MachineState targetState)
        {
            if (mStateDic.TryGetValue(id, out var state))
            {
                if (state.Status == targetState) return;
                switch (targetState)
                {
                    case MachineState.End:
                        if (state.Status is not MachineState.End)
                        {
                            state.State.End();
                            mStateDic[id] = (state.State, MachineState.End);
                        }
                        break;
                    case MachineState.Suspend:
                        if (state.Status is MachineState.Running)
                        {
                            state.State.Suspend();
                            mStateDic[id] = (state.State, MachineState.Suspend);
                        }
                        break;
                    case MachineState.Running:
                        if (state.Status is not MachineState.Running)
                        {
                            state.State.Start();
                            mStateDic[id] = (state.State, MachineState.Running);
                        }
                        break;
                }
            }
        }

        void IFSM<TEnum>.Change(TEnum id)
        {
            Change(id, MachineState.Running);
        }

        void IFSM<TEnum>.Change<TArgs>(TEnum id, TArgs args)
        {
            if (mStateDic.TryGetValue(id, out var state))
            {
                if (state.Status is MachineState.Running) return;
                if (state.State is IState<TArgs> stateWithArgs)
                    stateWithArgs.Start(args);
                else
                    state.State.Start();
                mStateDic[id] = (state.State, MachineState.Running);
            }
        }

        void IFSM<TEnum>.Start(TEnum id)
        {
            if (mStateDic.TryGetValue(id, out var state) && state.Status is not MachineState.Running)
            {
                mMachineState = MachineState.Running;
                state.State.Start();
                mStateDic[id] = (state.State, MachineState.Running);
            }
        }
        void IState.Dispose()
        {
#if UNITY_EDITOR || GODOT
            FsmEditorHook.OnFsmDisposed?.Invoke(this);
#endif
            Clear();
        }
        void IState.SendMessage<TMsg>(TMsg message)
        {
            if (mMachineState is not MachineState.Running) return;

            foreach (var state in mStateDic.Values)
            {
                if (state.Status is MachineState.Running)
                    state.State.SendMessage(message);
            }
        }
    }
}
