#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 在 Unity Editor 程序集加载后安装 ActionKit 工具交互能力。
    /// </summary>
    internal static class UnityActionKitEditorInstaller
    {
        /// <summary>
        /// 把 Unity 的 Editor 加载时机转发给跨宿主 ActionKit 编辑器安装入口。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Install()
        {
            ActionKitEditorInstaller.EnsureInstalled();
        }
    }
}
#endif
