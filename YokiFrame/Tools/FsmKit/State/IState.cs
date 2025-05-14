namespace YokiFrame
{
    public interface IState
    {
        virtual bool Condition() => true;
        void Start();
        void Suspend();
        void Update();
        void FixedUpdate();
        void CustomUpdate();
        void End();
    }

    public interface IState<TArgs> : IState
    {
        void IState.Start() => Start(default);
        void Start(TArgs args);
    }
}