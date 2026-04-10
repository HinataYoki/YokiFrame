#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 处理不同 Unity 版本下的样式表兼容路径选择。
    /// 低版本优先加载 Legacy 变体，避免使用 2021.3 不支持的 USS 属性。
    /// </summary>
    internal static class YokiStyleCompatibility
    {
        /// <summary>
        /// 当前是否需要启用 2021.3 兼容样式。
        /// </summary>
        public static bool UseLegacyStyleSheet
        {
            get
            {
#if UNITY_2022_1_OR_NEWER
                return false;
#else
                return true;
#endif
            }
        }

        /// <summary>
        /// 根据当前 Unity 版本解析最终应加载的样式表路径。
        /// </summary>
        public static string ResolveStyleSheetPath(string assetPath)
        {
            if (!UseLegacyStyleSheet || string.IsNullOrEmpty(assetPath))
            {
                return assetPath;
            }

            var legacyPath = GetLegacyPath(assetPath);
            if (string.IsNullOrEmpty(legacyPath))
            {
                return assetPath;
            }

            var legacySheet = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(legacyPath);
            return legacySheet != null ? legacyPath : assetPath;
        }

        /// <summary>
        /// 根据文件名解析兼容样式表文件名。
        /// 例如：BindInspectorStyles -> BindInspectorStyles.Legacy
        /// </summary>
        public static string ResolveStyleSheetName(string styleSheetName)
        {
            if (!UseLegacyStyleSheet || string.IsNullOrEmpty(styleSheetName) || styleSheetName.EndsWith(".Legacy"))
            {
                return styleSheetName;
            }

            var legacyName = $"{styleSheetName}.Legacy";
            var guids = AssetDatabase.FindAssets($"{legacyName} t:StyleSheet");
            return guids.Length > 0 ? legacyName : styleSheetName;
        }

        private static string GetLegacyPath(string assetPath)
        {
            var extension = Path.GetExtension(assetPath);
            if (string.IsNullOrEmpty(extension))
            {
                return null;
            }

            return assetPath.Substring(0, assetPath.Length - extension.Length) + ".Legacy" + extension;
        }
    }
}
#endif
