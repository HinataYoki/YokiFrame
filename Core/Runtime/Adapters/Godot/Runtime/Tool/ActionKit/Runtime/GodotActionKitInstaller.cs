#if GODOT
namespace YokiFrame.Godot
{
    /// <summary>
    /// Godot 侧 ActionKit 驱动入口，由 GodotBootstrap 的 _Process 统一调用。
    /// </summary>
    [YokiFrameKitDiscoverableInstaller(YokiFrameEngine.Godot)]
    public sealed class GodotActionKitInstaller : IYokiFrameKitInstaller
    {
        public string KitName
        {
            get { return "Godot.ActionKit"; }
        }

        public void Install(YokiFrameEngineContext context)
        {
            if (context.Engine != YokiFrameEngine.Godot)
                return;

            ActionKitScheduler.Initialize();
            ActionKitRuntimeLog.ErrorHandler = LogError;
        }

        public bool Tick(float deltaSeconds)
        {
            ActionKitScheduler.Tick(deltaSeconds, deltaSeconds);
            ActionKitScheduler.ProcessRecycle();
            return true;
        }

        public void Shutdown()
        {
            ActionKitRuntimeLog.ErrorHandler = null;
        }

        private static void LogError(string message)
        {
            global::Godot.GD.PushError(message);
        }
    }
}
#endif
