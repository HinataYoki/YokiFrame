#if !GODOT
namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity 侧 ActionKit 驱动安装入口，供 UnityBootstrap 反射调用。
    /// </summary>
    public static class UnityActionKitInstaller
    {
        public static void Install(IResourceProvider resourceProvider)
        {
            UnityActionKitPlayerLoopSystem.Initialize();
            ActionKitRuntimeLog.ErrorHandler = UnityActionKitPlayerLoopSystem.LogError;
        }
    }
}
#endif
