#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 工具菜单入口
    /// 所有 [MenuItem] 集中在此文件，便于 AI 快速定位入口
    /// </summary>
    public static class YokiToolsMenu
    {
        private const string MENU_ROOT = "YokiFrame/";
        
        /// <summary>
        /// 打开工具面板（主入口）
        /// </summary>
        [MenuItem(MENU_ROOT + "Tools Panel %e", priority = 0)]
        public static void Open()
        {
            YokiToolsWindow.Open();
        }
        
        /// <summary>
        /// 打开工具面板并选择指定页面
        /// </summary>
        /// <typeparam name="T">页面类型</typeparam>
        /// <param name="onPageSelected">页面选中后的回调</param>
        public static void OpenAndSelectPage<T>(System.Action<T> onPageSelected = null) 
            where T : class, IYokiToolPage
        {
            YokiToolsWindow.OpenAndSelectPage(onPageSelected);
        }
    }
}
#endif
