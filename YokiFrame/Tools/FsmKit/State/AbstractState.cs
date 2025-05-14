using System;

namespace YokiFrame
{
    public abstract class AbstractState<TEnum, TBlack> : IState where TEnum : Enum
    {
        protected FSM<TEnum> mFSM;
        protected TBlack mBlack;
        public AbstractState(FSM<TEnum> fsm, TBlack black)
        {
            mFSM = fsm; 
            mBlack = black;
        }

        protected virtual bool OnCondition() => true;
        protected virtual void OnEnter() { }
        protected virtual void OnUpdate() { }
        protected virtual void OnFixedUpdate() { }
        protected virtual void OnCustomUpdate() { }
        protected virtual void OnExit() { }
        protected virtual void OnSuspend() { }


        bool IState.Condition() => OnCondition();
        void IState.Start() => OnEnter();
        void IState.Update() => OnUpdate();
        void IState.FixedUpdate() => OnFixedUpdate();
        void IState.End() => OnExit();
        void IState.Suspend() => OnSuspend();
        void IState.CustomUpdate() => OnCustomUpdate();
    }

    public abstract class AbstractState<TEnum, TBlack, TArgs> : AbstractState<TEnum, TBlack>, IState<TArgs> where TEnum : Enum
    {
        protected AbstractState(FSM<TEnum> fsm, TBlack black) : base(fsm, black)
        {
        }
        protected sealed override void OnEnter() => OnEnter(default);
        protected virtual void OnEnter(TArgs args) { }

        bool IState.Condition() => OnCondition();
        void IState<TArgs>.Start(TArgs args) => OnEnter(args);
        void IState.Update() => OnUpdate();
        void IState.FixedUpdate() => OnFixedUpdate();
        void IState.CustomUpdate() => OnCustomUpdate();
        void IState.End() => OnExit();
        void IState.Suspend() => OnSuspend();
    }
}