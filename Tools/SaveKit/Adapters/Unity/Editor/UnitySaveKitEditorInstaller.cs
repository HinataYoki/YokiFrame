#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame.Unity
{
    /// <summary>在 Unity Editor 程序集加载后安装 SaveKit Tool Provider。</summary>
    internal static class UnitySaveKitEditorInstaller
    {
        /// <summary>把 Unity Editor 的加载时机转发给跨宿主 SaveKit 安装入口。</summary>
        [InitializeOnLoadMethod]
        private static void Install()
        {
            YokiFrame.SaveKitEditorInstaller.EnsureInstalled();
        }
    }
}
#endif
