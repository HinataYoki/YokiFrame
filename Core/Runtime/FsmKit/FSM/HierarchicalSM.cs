using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 层级状态机，可以包含多个状态机，每个状态机可以独立运行
    /// </summary>
    public class HierarchicalSM<TEnum> : IFSM<TEnum> where TEnum : Enum
    {
        private readonly SortedDictionary<TEnum, (IState State, MachineState Status)> mStateDic = new();

        /// <summary>
        /// 缓存的 key 列表，用于状态更新时避免遍历中修改字典
        /// </summary>
        private readonly List<TEnum> mTempKeys = new(8);

        private MachineState machineState = MachineState.End;
        public MachineState MachineState => machineState;
        public TEnum CurEnum { get; private set; }
        
#if UNITY_EDITOR
        private Dictionary<int, IState> mStateIdCache;
        // IFSM 接口实现
        public string Name { get; set; }
        public Type EnumType => typeof(TEnum);
        IState IFSM.CurrentState => mStateDic.TryGetValue(CurEnum, out var s) ? s.State : null;
        int IFSM.CurrentStateId => CurEnum != null ? Convert.ToInt32(CurEnum) : -1;
#endif

        public HierarchicalSM(string name = null)
        {
#if UNITY_EDITOR
            Name = name ?? $"HierarchicalSM<{typeof(TEnum).Name}>";
#endif
        }

#if UNITY_EDITOR
        public IReadOnlyDictionary<int, IState> GetAllStates()
        {
            mStateIdCache ??= new();
            mStateIdCache.Clear();
            foreach (var kvp in mStateDic)
                mStateIdCache[Convert.ToInt32(kvp.Key)] = kvp.Value.State;
            return mStateIdCache;
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
                {
                    statePair.State.End();
                }
                statePair.State.Dispose();
                mStateDic.Remove(id);
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
        }

        public void Clear()
        {
            foreach (var state in mStateDic.Values)
            {
                if (state.Status is not MachineState.End)
                {
                    state.State.End();
                }
                state.State.Dispose();
            }
            mStateDic.Clear();
        }

        public void CustomUpdate()
        {
            if (machineState is MachineState.Running)
            {
                foreach (var state in mStateDic.Values)
                {
                    if (state.Status is MachineState.Running)
                    {
                        state.State.CustomUpdate();
                    }
                }
            }
        }

        public void End()
        {
            machineState = MachineState.End;
            // ValueTuple 是值类型，遍历 .Values 获取的是副本，需收集 key 后写回
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
            if (machineState is MachineState.Running)
            {
                foreach (var state in mStateDic.Values)
                {
                    if (state.Status is MachineState.Running)
                    {
                        state.State.FixedUpdate();
                    }
                }
            }
        }

        public void Remove(TEnum id)
        {
            if (mStateDic.TryGetValue(id, out var state))
            {
                if (state.Status is not MachineState.End)
                {
                    state.State.End();
                }
                state.State.Dispose();
                mStateDic.Remove(id);
            }
        }

        public void Start()
        {
            machineState = MachineState.Running;
            // ValueTuple 是值类型，遍历 .Values 获取的是副本，需收集 key 后写回
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
            if (machineState is MachineState.Running)
            {
                machineState = MachineState.Suspend;
                // ValueTuple 是值类型，遍历 .Values 获取的是副本，需收集 key 后写回
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
            if (machineState is MachineState.Running)
            {
                foreach (var state in mStateDic.Values)
                {
                    if (state.Status is MachineState.Running)
                    {
                        state.State.Update();
                    }
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

        void IFSM<TEnum>.Change(TEnum id) { }

        void IFSM<TEnum>.Change<TArgs>(TEnum id, TArgs args) { }

        void IFSM<TEnum>.Start(TEnum id) { }

        void IState.Dispose() => Clear();

        void IState.SendMessage<TMsg>(TMsg message) { }
    }
}