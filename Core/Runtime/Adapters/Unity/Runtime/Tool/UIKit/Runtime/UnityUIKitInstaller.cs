#if !GODOT
namespace YokiFrame.Unity
{
    /// <summary>
    /// UIKit 使用场景中的 UIRoot 作为运行时入口，Unity 侧无需额外注入 backend。
    /// </summary>
    public static class UnityUIKitInstaller
    {
        public static void Install(IResourceProvider provider)
        {
        }
    }
}
#endif
