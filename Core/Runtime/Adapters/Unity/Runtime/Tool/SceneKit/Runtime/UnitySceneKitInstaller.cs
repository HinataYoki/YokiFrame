#if !GODOT
using YokiFrame;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 将 Unity SceneManager 后端注入到 SceneKit，业务侧仍使用统一静态入口。
    /// </summary>
    public static class UnitySceneKitInstaller
    {
        public static void Install(IResourceProvider provider)
        {
            ResKit.SetSceneBackend(new UnitySceneBackend());
        }
    }
}
#endif
