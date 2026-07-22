#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame.Unity
{
    /// <summary>在 Unity Editor 程序集加载后安装 AudioKit 工具 Provider。</summary>
    internal static class UnityAudioKitEditorInstaller
    {
        /// <summary>把 Unity Editor 加载时机转发给跨宿主安装入口。</summary>
        [InitializeOnLoadMethod]
        private static void Install() => AudioKitEditorInstaller.EnsureInstalled();
    }
}
#endif
