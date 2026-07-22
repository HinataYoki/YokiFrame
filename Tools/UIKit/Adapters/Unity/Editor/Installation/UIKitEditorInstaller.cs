#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame
{
    /// <summary>把 Unity UIKit 只读 Provider 安装到共享 Tool catalog。</summary>
    public static class UIKitEditorInstaller
    {
        private static readonly UIKitInteractionProvider sProvider = new();

        /// <summary>Unity Editor 程序集加载后幂等注册 UIKit Provider。</summary>
        [InitializeOnLoadMethod]
        private static void InstallOnEditorLoad()
        {
            EnsureInstalled();
        }

        /// <summary>幂等注册 UIKit 只读 Provider，供测试和显式重装入口复用。</summary>
        public static void EnsureInstalled()
        {
            YokiFrameToolKitInteractionCatalog.Register(sProvider);
        }
    }
}
#endif
