namespace YokiFrame
{
    /// <summary>
    /// 定义 FsmKit 状态的同步生命周期；状态由调用方或宿主手动驱动。
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// 判断当前状态是否允许进入；默认允许。
        /// </summary>
        /// <returns>允许进入时返回 true。</returns>
        bool Condition()
        {
            return true;
        }

        /// <summary>进入状态。</summary>
        void Start();

        /// <summary>暂停状态。</summary>
        void Suspend();

        /// <summary>执行普通帧更新。</summary>
        void Update();

        /// <summary>执行固定步长更新。</summary>
        void FixedUpdate();

        /// <summary>执行调用方定义的自定义更新。</summary>
        void CustomUpdate();

        /// <summary>结束状态。</summary>
        void End();

        /// <summary>释放状态持有的资源；状态机保证每次移除只调用一次。</summary>
        void Dispose();

        /// <summary>
        /// 向状态发送强类型消息。
        /// </summary>
        /// <typeparam name="TMsg">消息类型。</typeparam>
        /// <param name="message">消息值。</param>
        void SendMessage<TMsg>(TMsg message);
    }

    /// <summary>
    /// 定义需要进入参数的状态；无参进入会按 2.0-pre 语义传入默认值。
    /// </summary>
    /// <typeparam name="TArgs">进入参数类型。</typeparam>
    public interface IState<TArgs> : IState
    {
        /// <summary>
        /// 把无参进入映射为默认参数进入，保持普通 FSM 与带参 FSM 可互换。
        /// </summary>
        void IState.Start()
        {
            Start(default(TArgs));
        }

        /// <summary>
        /// 使用指定参数进入状态。
        /// </summary>
        /// <param name="args">进入参数。</param>
        void Start(TArgs args);
    }
}
