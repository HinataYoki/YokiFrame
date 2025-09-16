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
            }
        }

        public void Change(TEnum id)
        {
            if (machineState is not MachineState.Running) return;
            if (mStateDic.TryGetValue(id, out var state))
            {
                if (state != CurState && state.Condition())
                {
                    CurState?.End();
                    CurState = state;
                    CurEnum = id;
                    state.Start();
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

        void IState.Dispose() => Clear();
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
