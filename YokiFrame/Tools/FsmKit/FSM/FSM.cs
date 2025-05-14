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
        protected readonly Dictionary<TEnum, IState> mStateDic = new();

        public void Add(TEnum id, IState state)
        {
            if (mStateDic.ContainsKey(id))
            {
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
                if (CurState == state) CurState = null;
                mStateDic.Remove(id);
            }
        }

        public void Change(TEnum id)
        {
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
            mStateDic.Clear();
            CurState = null;
            mMachineState = MachineState.End;
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
            mMachineState = MachineState.End;
            CurState?.End();
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
            if (CurState != null)
            {
                mMachineState = MachineState.Running;
                CurState.Start();
            }
        }

        public void Start(TEnum id)
        {
            if (mStateDic.TryGetValue(id, out var state))
            {
                mMachineState = MachineState.Running;
                CurState = state;
                CurEnum = id;
                state.Start();
            }
        }

        public void Suspend()
        {
            if (mMachineState is MachineState.Running)
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
    }

    public class FSM<TEnum, TArgs> : FSM<TEnum>, IFSM<TEnum, TArgs> where TEnum : Enum
    {
        public void Start(TArgs args)
        {
            if (CurState != null)
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
            if (mStateDic.TryGetValue(id, out var state))
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