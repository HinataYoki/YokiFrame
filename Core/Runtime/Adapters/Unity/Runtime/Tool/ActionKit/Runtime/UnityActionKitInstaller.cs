#if !GODOT
namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity 侧 ActionKit 驱动安装入口，供 UnityBootstrap 反射调用。
    /// </summary>
    [YokiFrameKitDiscoverableInstaller(YokiFrameEngine.Unity)]
    public sealed class UnityActionKitInstaller : IYokiFrameKitInstaller
    {
        public string KitName
        {
            get { return "Unity.ActionKit"; }
        }

        public void Install(YokiFrameEngineContext context)
        {
            if (context.Engine != YokiFrameEngine.Unity)
                return;

            UnityActionKitPlayerLoopSystem.Initialize();
            ActionKitRuntimeLog.ErrorHandler = UnityActionKitPlayerLoopSystem.LogError;
        }

        public bool Tick(float deltaSeconds)
        {
            return true;
        }

        public void Shutdown()
        {
            ActionKitRuntimeLog.ErrorHandler = null;
        }
    }
}
#endif
