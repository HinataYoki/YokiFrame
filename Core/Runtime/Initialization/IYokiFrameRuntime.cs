namespace YokiFrame
{
    public interface IYokiFrameRuntime
    {
        YokiFrameEngineContext Context { get; }

        void Tick(float deltaSeconds);

        void Shutdown();
    }
}
