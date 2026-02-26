using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 层级状态机，可以包含多个状态机，每个状态机可以独立运行
    /// </summary>
    public class HierarchicalSM<TEnum> : IFSM<TEnum> where TEnum : Enum
    {
        private readonly SortedDictionary<TEnum, (IState, MachineState)> mStateDic = new();

        private MachineState machineState = MachineState.End;
        public MachineState MachineState => machineState;
        public TEnum CurEnum { get; private set; }
        
#if UNITY_EDITOR
        private Dictionary<int, IState> mStateIdCache;
        // IFSM 接口实现
        public string Name { get; set; }
        public Type EnumType => typeof(TEnum);
        IState IFSM.CurrentState => mStateDic.TryGetValue(CurEnum, out var s) ? s.Item1 : null;
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
                mStateIdCache[Convert.ToInt32(kvp.Key)] = kvp.Value.Item1;
            return mStateIdCache;
        }
#endif

        public void Get(TEnum id, out IState state)
        {
            mStateDic.TryGetValue(id, out var statePair);
            state = statePair.Item1;
        }

        public void Add(TEnum id, IState state)
        {
            if (mStateDic.TryGetValue(id, out var statePair))
            {
                if (statePair.Item2 is not MachineState.End)
                {
                    statePair.Item1.End();
                }
                statePair.Item1.Dispose();
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
                if (state.Item2 is not MachineState.End)
                {
                    state.Item1.End();
                }
                state.Item1.Dispose();
            }
            mStateDic.Clear();
        }

        public void CustomUpdate()
        {
            if (machineState is MachineState.Running)
            {
                foreach (var state in mStateDic.Values)
                {
                    if (state.Item2 is MachineState.Running)
                    {
                        state.Item1.CustomUpdate();
                    }
                }
            }
        }

        public void End()
        {
            machineState = MachineState.End;
            foreach (var state in mStateDic.Values)
            {
                if (state.Item2 is not MachineState.End)
                {
                    state.Item1.End();
                }
            }
        }

        public void FixedUpdate()
        {
            if (machineState is MachineState.Running)
            {
                foreach (var state in mStateDic.Values)
                {
                    if (state.Item2 is MachineState.Running)
                    {
                        state.Item1.FixedUpdate();
                    }
                }
            }
        }

        public void Remove(TEnum id)
        {
            if (mStateDic.TryGetValue(id, out var state))
            {
                if (state.Item2 is not MachineState.End)
                {
                    state.Item1.End();
                }
                state.Item1.Dispose();
                mStateDic.Remove(id);
            }
        }

        public void Start()
        {
            machineState = MachineState.Running;
            foreach (var state in mStateDic.Values)
            {
                if (state.Item2 is not MachineState.Running)
                {
                    state.Item1.Start();
                }
            }
        }

        public void Suspend()
        {
            if (machineState is MachineState.Running)
            {
                machineState = MachineState.Suspend;
                foreach (var state in mStateDic.Values)
                {
                    if (state.Item2 is MachineState.Running)
                    {
                        state.Item1.Suspend();
                    }
                }
            }
        }

        public void Update()
        {
            if (machineState is MachineState.Running)
            {
                foreach (var state in mStateDic.Values)
                {
                    if (state.Item2 is MachineState.Running)
                    {
                        state.Item1.Update();
                    }
                }
            }
        }

        public void Change(TEnum id, MachineState machineState)
        {
            if (mStateDic.TryGetValue(id, out var state))
            {
                switch (machineState)
                {
                    case MachineState.End:
                        if (state.Item2 is not MachineState.End)
                        {
                            state.Item1.End();
                        }
                        break;
                    case MachineState.Suspend:
                        if (state.Item2 is MachineState.Running)
                        {
                            state.Item1.Suspend();
                        }
                        break;
                    case MachineState.Running:
                        if (state.Item2 is not MachineState.Running)
                        {
                            state.Item1.Start();
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