#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using UnityEditor;

namespace YokiFrame.Unity
{
    /// <summary>
    /// YooAsset 编辑器菜单桥接。2.x 和 3.x 菜单命名不同，统一从这里分发。
    /// </summary>
    public static class YooAssetEditorMenuBridge
    {
#if YOOASSET_3_0_OR_NEWER
        /// <summary>
        /// 当前 YooAsset 版本的收集器菜单路径。
        /// </summary>
        public const string COLLECTOR_MENU_PATH = "YooAsset/Bundle Collector";

        /// <summary>
        /// 当前 YooAsset 版本的构建器菜单路径。
        /// </summary>
        public const string BUILDER_MENU_PATH = "YooAsset/Bundle Builder";

        /// <summary>
        /// 当前 YooAsset 版本的调试器菜单路径。
        /// </summary>
        public const string DEBUGGER_MENU_PATH = "YooAsset/Bundle Debugger";
#else
        /// <summary>
        /// 当前 YooAsset 版本的收集器菜单路径。
        /// </summary>
        public const string COLLECTOR_MENU_PATH = "YooAsset/AssetBundle Collector";

        /// <summary>
        /// 当前 YooAsset 版本的构建器菜单路径。
        /// </summary>
        public const string BUILDER_MENU_PATH = "YooAsset/AssetBundle Builder";

        /// <summary>
        /// 当前 YooAsset 版本的调试器菜单路径。
        /// </summary>
        public const string DEBUGGER_MENU_PATH = "YooAsset/AssetBundle Debugger";
#endif

        private const string COLLECTOR_FALLBACK_MENU_PATH = "YooAsset/AssetBundle Collector";
        private const string BUILDER_FALLBACK_MENU_PATH = "YooAsset/AssetBundle Builder";
        private const string DEBUGGER_FALLBACK_MENU_PATH = "YooAsset/AssetBundle Debugger";
        private const string COLLECTOR_V3_FALLBACK_MENU_PATH = "YooAsset/Bundle Collector";
        private const string BUILDER_V3_FALLBACK_MENU_PATH = "YooAsset/Bundle Builder";
        private const string DEBUGGER_V3_FALLBACK_MENU_PATH = "YooAsset/Bundle Debugger";

        /// <summary>
        /// 打开 YooAsset 收集器窗口。
        /// </summary>
        /// <returns>成功执行菜单时返回 true。</returns>
        public static bool OpenCollector()
            => ExecuteFirstAvailable(COLLECTOR_MENU_PATH, COLLECTOR_FALLBACK_MENU_PATH, COLLECTOR_V3_FALLBACK_MENU_PATH);

        /// <summary>
        /// 打开 YooAsset 构建器窗口。
        /// </summary>
        /// <returns>成功执行菜单时返回 true。</returns>
        public static bool OpenBuilder()
            => ExecuteFirstAvailable(BUILDER_MENU_PATH, BUILDER_FALLBACK_MENU_PATH, BUILDER_V3_FALLBACK_MENU_PATH);

        /// <summary>
        /// 打开 YooAsset 调试器窗口。
        /// </summary>
        /// <returns>成功执行菜单时返回 true。</returns>
        public static bool OpenDebugger()
            => ExecuteFirstAvailable(DEBUGGER_MENU_PATH, DEBUGGER_FALLBACK_MENU_PATH, DEBUGGER_V3_FALLBACK_MENU_PATH);

        private static bool ExecuteFirstAvailable(params string[] menuPaths)
        {
            for (var i = 0; i < menuPaths.Length; i++)
            {
                if (EditorApplication.ExecuteMenuItem(menuPaths[i]))
                    return true;
            }

            return false;
        }
    }
}
#endif
#endif
