#if !GODOT
namespace YokiFrame.Unity
{
    /// <summary>
    /// UIKit 使用场景中的 UIRoot 作为运行时入口，Unity 侧无需额外注入 backend。
    /// </summary>
    [YokiFrameKitDiscoverableInstaller(YokiFrameEngine.Unity)]
    public sealed class UnityUIKitInstaller : IYokiFrameKitInstaller
    {
        public string KitName
        {
            get { return "Unity.UIKit"; }
        }

        public void Install(YokiFrameEngineContext context)
        {
            Install(context.GetService<IResourceProvider>());
        }

        public static void Install(IResourceProvider provider)
        {
        }

        public bool Tick(float deltaSeconds)
        {
            return true;
        }

        public void Shutdown()
        {
        }
    }
}
#endif
