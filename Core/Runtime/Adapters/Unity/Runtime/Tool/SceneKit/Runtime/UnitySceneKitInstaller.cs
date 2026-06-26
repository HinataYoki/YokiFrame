#if !GODOT
using YokiFrame;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 将 Unity SceneManager 后端注入到 SceneKit，业务侧仍使用统一静态入口。
    /// </summary>
    [YokiFrameKitDiscoverableInstaller(YokiFrameEngine.Unity)]
    public sealed class UnitySceneKitInstaller : IYokiFrameKitInstaller
    {
        public string KitName
        {
            get { return "Unity.SceneKit"; }
        }

        public void Install(YokiFrameEngineContext context)
        {
            if (context.Engine != YokiFrameEngine.Unity)
                return;

            Install(context.GetService<IResourceProvider>());
        }

        public static void Install(IResourceProvider provider)
        {
            if (ResKit.GetSceneBackend() == null)
            {
                var sceneBackend = provider as IResSceneBackend;
                ResKit.SetSceneBackend(sceneBackend ?? new UnitySceneBackend());
            }
        }

        public bool Tick(float deltaSeconds)
        {
            return true;
        }

        public void Shutdown()
        {
            ResKit.ClearSceneBackend();
        }
    }
}
#endif
