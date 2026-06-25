#if !GODOT
using YokiFrame;
using UIKitApi = YokiFrame.UIKit;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 将 Unity UI 后端注入 UIKit，保持跨引擎静态入口一致。
    /// </summary>
    public static class UnityUIKitInstaller
    {
        public static void Install(IResourceProvider provider)
        {
            UIKitApi.SetBackend(new UnityUIBackend());
        }
    }
}
#endif
