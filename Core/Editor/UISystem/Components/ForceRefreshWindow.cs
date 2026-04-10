#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 临时工具：强制刷新 YokiFrame Tools Window
    /// 用于解决 UI 更新不生效的问题
    /// </summary>
    public static class ForceRefreshWindow
    {
        [MenuItem("YokiFrame/Debug/Force Refresh Tools Window")]
        public static void RefreshWindow()
        {
            // 关闭所有 YokiToolsWindow 实例
            var windows = Resources.FindObjectsOfTypeAll<YokiToolsWindow>();
            foreach (var window in windows)
            {
                if (window != default)
                {
                    window.Close();
                }
            }

            // 清除 UIElements 缓存
            EditorUtility.ClearProgressBar();
            
            // 重新打开窗口
            EditorApplication.delayCall += () =>
            {
                YokiToolsWindow.Open();
            };
        }

        [MenuItem("YokiFrame/Debug/Clear UIElements Cache")]
        public static void ClearUIElementsCache()
        {
            var libraryPath = System.IO.Path.Combine(Application.dataPath, "../Library/UIElements");
            if (System.IO.Directory.Exists(libraryPath))
            {
                try
                {
                    System.IO.Directory.Delete(libraryPath, true);
                    EditorUtility.DisplayDialog(
                        "清理完成",
                        "UIElements 缓存已删除，请重启 Unity 编辑器以应用更改。",
                        "确定");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ForceRefreshWindow] 删除缓存失败：{e.Message}");
                }
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "无需清理",
                    "未找到 UIElements 缓存目录。",
                    "确定");
            }
        }
    }
}
#endif
