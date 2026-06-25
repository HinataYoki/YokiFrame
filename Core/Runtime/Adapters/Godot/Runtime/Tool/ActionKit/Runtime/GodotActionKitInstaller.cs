#if GODOT
namespace YokiFrame.Godot
{
    /// <summary>
    /// Godot 侧 ActionKit 驱动入口，由 GodotBootstrap 的 _Process 统一调用。
    /// </summary>
    public static class GodotActionKitInstaller
    {
        public static void Install(IResourceProvider resourceProvider)
        {
            ActionKitScheduler.Initialize();
            ActionKitRuntimeLog.ErrorHandler = LogError;
        }

        public static bool TickActionKit(float deltaSeconds)
        {
            ActionKitScheduler.Tick(deltaSeconds, deltaSeconds);
            ActionKitScheduler.ProcessRecycle();
            return true;
        }

        private static void LogError(string message)
        {
            global::Godot.GD.PushError(message);
        }
    }
}
#endif
