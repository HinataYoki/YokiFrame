#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame.Unity
{
    /// <summary>在 Unity Editor 程序集加载后安装 SpatialKit Tool Provider。</summary>
    internal static class UnitySpatialKitEditorInstaller
    {
        /// <summary>把 Unity Editor 加载时机转发给跨宿主 SpatialKit 安装入口。</summary>
        [InitializeOnLoadMethod]
        private static void Install()
        {
            YokiFrame.SpatialKitEditorInstaller.EnsureInstalled();
        }
    }
}
#endif
