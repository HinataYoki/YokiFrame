namespace YokiFrame
{
    /// <summary>
    /// Kit 默认后端安装器。各 Kit 按需根据当前引擎上下文安装自己的默认实现。
    /// </summary>
    public interface IYokiFrameKitInstaller
    {
        string KitName { get; }

        void Install(YokiFrameEngineContext context);

        bool Tick(float deltaSeconds);

        void Shutdown();
    }
}
