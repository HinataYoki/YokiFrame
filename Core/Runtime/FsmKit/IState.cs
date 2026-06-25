namespace YokiFrame
{
    /// <summary>有限状态机生命周期状态。</summary>
    public enum MachineState
    {
        End = 0,
        Suspend = 1,
        Running = 2
    }

    public interface IState
    {
        virtual bool Condition() => true;
        void Start();
        void Suspend();
        void Update();
        void FixedUpdate();
        void CustomUpdate();
        void End();
        void Dispose();
        void SendMessage<TMsg>(TMsg message);
    }

    public interface IState<TArgs> : IState
    {
        void IState.Start() => Start(default);
        void Start(TArgs args);
    }
}
