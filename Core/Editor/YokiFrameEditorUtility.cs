#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 编辑器工具类 - 提供路径查找等通用功能
    /// </summary>
    public static class YokiFrameEditorUtility
    {
        private static string sCachedRootPath;
        
        /// <summary>
        /// 获取 YokiFrame 根目录路径（相对于 Assets）
        /// </summary>
        public static string GetYokiFrameRootPath()
        {
            if (!string.IsNullOrEmpty(sCachedRootPath) && Directory.Exists(sCachedRootPath))
                return sCachedRootPath;
            
            // 通过查找标志性文件来定位 YokiFrame 目录
            var guids = AssetDatabase.FindAssets("YokiFrameToolStyles t:StyleSheet");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                // path = "Assets/.../YokiFrame/Core/Editor/Styles/YokiFrameToolStyles.uss"
                // 需要回退到 YokiFrame 目录
                var dir = Path.GetDirectoryName(path); // Styles
                dir = Path.GetDirectoryName(dir);       // Editor
                dir = Path.GetDirectoryName(dir);       // Core
                dir = Path.GetDirectoryName(dir);       // YokiFrame
                sCachedRootPath = dir?.Replace('\\', '/');
                return sCachedRootPath;
            }
            
            // 回退：使用默认路径
            sCachedRootPath = "Assets/YokiFrame";
            return sCachedRootPath;
        }
        
        /// <summary>
        /// 获取主样式表路径
        /// </summary>
        public static string GetMainStyleSheetPath()
        {
            return $"{GetYokiFrameRootPath()}/Core/Editor/Styles/YokiFrameToolStyles.uss";
        }
        
        /// <summary>
        /// 加载主样式表
        /// </summary>
        public static StyleSheet LoadMainStyleSheet()
        {
            var path = GetMainStyleSheetPath();
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            
            if (styleSheet == null)
            {
                Debug.LogWarning($"[YokiFrame] 无法加载样式表: {path}");
            }
            
            return styleSheet;
        }
        
        /// <summary>
        /// 为 VisualElement 应用主样式表
        /// </summary>
        public static void ApplyMainStyleSheet(VisualElement root)
        {
            var styleSheet = LoadMainStyleSheet();
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }
        }
        
        /// <summary>
        /// 通过文件名查找并加载样式表
        /// </summary>
        /// <param name="ussFileName">USS 文件名（不含路径，如 "BindInspectorStyles"）</param>
        public static StyleSheet LoadStyleSheetByName(string ussFileName)
        {
            var guids = AssetDatabase.FindAssets($"{ussFileName} t:StyleSheet");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            }
            
            Debug.LogWarning($"[YokiFrame] 无法找到样式表: {ussFileName}");
            return null;
        }
        
        /// <summary>
        /// 清除缓存（用于路径变更后刷新）
        /// </summary>
        public static void ClearCache()
        {
            sCachedRootPath = null;
        }
    }
}
#endif
