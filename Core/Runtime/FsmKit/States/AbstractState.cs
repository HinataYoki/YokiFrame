using System;

namespace YokiFrame
{
    /// <summary>
    /// 为共享黑板的状态提供可覆写生命周期，并隐藏 IState 的显式实现样板。
    /// </summary>
    /// <typeparam name="TEnum">所属 FSM 的状态枚举类型。</typeparam>
    /// <typeparam name="TBlack">共享黑板类型。</typeparam>
    public abstract class AbstractState<TEnum, TBlack> : IState where TEnum : Enum
    {
        /// <summary>所属状态机，供派生状态发起切换。</summary>
        protected FSM<TEnum> mFSM;

        /// <summary>状态共享的业务黑板。</summary>
        protected TBlack mBlack;

        /// <summary>
        /// 创建绑定状态机和黑板的状态。
        /// </summary>
        /// <param name="fsm">所属状态机。</param>
        /// <param name="black">共享黑板。</param>
        protected AbstractState(FSM<TEnum> fsm, TBlack black)
        {
            mFSM = fsm ?? throw new ArgumentNullException(nameof(fsm));
            mBlack = black;
        }

        /// <summary>判断状态是否允许进入；默认允许。</summary>
        /// <returns>允许进入时返回 true。</returns>
        protected virtual bool OnCondition() => true;

        /// <summary>处理状态进入。</summary>
        protected virtual void OnEnter() { }

        /// <summary>处理普通帧更新。</summary>
        protected virtual void OnUpdate() { }

        /// <summary>处理固定步长更新。</summary>
        protected virtual void OnFixedUpdate() { }

        /// <summary>处理调用方定义的自定义更新。</summary>
        protected virtual void OnCustomUpdate() { }

        /// <summary>处理状态结束。</summary>
        protected virtual void OnExit() { }

        /// <summary>处理状态暂停。</summary>
        protected virtual void OnSuspend() { }

        /// <summary>释放状态资源。</summary>
        protected virtual void OnDispose() { }

        /// <summary>处理强类型消息。</summary>
        /// <typeparam name="TMsg">消息类型。</typeparam>
        /// <param name="message">消息值。</param>
        protected virtual void OnMessage<TMsg>(TMsg message) { }

        /// <summary>把进入条件转发给派生状态。</summary>
        /// <returns>派生状态的判断结果。</returns>
        bool IState.Condition() => OnCondition();

        /// <summary>把无参进入转发给派生状态。</summary>
        void IState.Start() => OnEnter();

        /// <summary>把暂停转发给派生状态。</summary>
        void IState.Suspend() => OnSuspend();

        /// <summary>把普通更新转发给派生状态。</summary>
        void IState.Update() => OnUpdate();

        /// <summary>把固定更新转发给派生状态。</summary>
        void IState.FixedUpdate() => OnFixedUpdate();

        /// <summary>把自定义更新转发给派生状态。</summary>
        void IState.CustomUpdate() => OnCustomUpdate();

        /// <summary>把结束转发给派生状态。</summary>
        void IState.End() => OnExit();

        /// <summary>把释放转发给派生状态。</summary>
        void IState.Dispose() => OnDispose();

        /// <summary>把消息转发给派生状态。</summary>
        /// <typeparam name="TMsg">消息类型。</typeparam>
        /// <param name="message">消息值。</param>
        public void SendMessage<TMsg>(TMsg message) => OnMessage(message);
    }

    /// <summary>
    /// 为需要进入参数的共享黑板状态提供强类型 OnEnter 回调。
    /// </summary>
    /// <typeparam name="TEnum">所属 FSM 的状态枚举类型。</typeparam>
    /// <typeparam name="TBlack">共享黑板类型。</typeparam>
    /// <typeparam name="TArgs">进入参数类型。</typeparam>
    public abstract class AbstractState<TEnum, TBlack, TArgs> : AbstractState<TEnum, TBlack>, IState<TArgs>
        where TEnum : Enum
    {
        /// <summary>创建绑定状态机和黑板的带参状态。</summary>
        /// <param name="fsm">所属状态机。</param>
        /// <param name="black">共享黑板。</param>
        protected AbstractState(FSM<TEnum> fsm, TBlack black) : base(fsm, black) { }

        /// <summary>把无参进入密封为默认参数进入。</summary>
        protected sealed override void OnEnter() => OnEnter(default(TArgs));

        /// <summary>处理带参状态进入。</summary>
        /// <param name="args">进入参数。</param>
        protected virtual void OnEnter(TArgs args) { }

        /// <summary>把强类型进入转发给派生状态。</summary>
        /// <param name="args">进入参数。</param>
        void IState<TArgs>.Start(TArgs args) => OnEnter(args);
    }
}
