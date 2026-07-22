#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using UnityEditor;

namespace YokiFrame.Unity
{
    /// <summary>集中管理 YooAsset 官方 EditorWindow 菜单入口。</summary>
    internal static class YooAssetEditorWindows
    {
#if YOKIFRAME_YOOASSET_3
        private const string COLLECTOR_MENU = "YooAsset/Bundle Collector";
        private const string BUILDER_MENU = "YooAsset/Bundle Builder";
#else
        private const string COLLECTOR_MENU = "YooAsset/AssetBundle Collector";
        private const string BUILDER_MENU = "YooAsset/AssetBundle Builder";
#endif

        /// <summary>打开 YooAsset 官方资源收集器。</summary>
        internal static void OpenCollector()
        {
            OpenWindow(COLLECTOR_MENU, "资源收集器");
        }

        /// <summary>打开 YooAsset 官方资源构建器。</summary>
        internal static void OpenBuilder()
        {
            OpenWindow(BUILDER_MENU, "资源构建器");
        }

        /// <summary>执行官方菜单，并在入口缺失时显示明确错误。</summary>
        private static void OpenWindow(string menuPath, string displayName)
        {
            if (EditorApplication.ExecuteMenuItem(menuPath))
                return;

            EditorUtility.DisplayDialog(
                "YooAsset",
                "未找到 YooAsset " + displayName + "菜单，请检查 YooAsset Editor 包。",
                "确定");
        }
    }
}
#endif
