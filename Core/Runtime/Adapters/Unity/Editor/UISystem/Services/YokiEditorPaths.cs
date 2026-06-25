#if !GODOT
#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace YokiFrame.Unity
{
    public static class YokiEditorPaths
    {
        private const string UNITY_ROOT_RELATIVE_PATH = "Core/Runtime/Adapters/Unity";
        private const string EDITOR_ROOT_RELATIVE_PATH = UNITY_ROOT_RELATIVE_PATH + "/Editor";
        private const string DEFAULT_UNITY_ROOT = "Assets/YokiFrame/" + UNITY_ROOT_RELATIVE_PATH;
        private const string DEFAULT_EDITOR_ROOT = "Assets/YokiFrame/" + EDITOR_ROOT_RELATIVE_PATH;
        private static string sCachedEditorRoot;

        public static string GetEditorRoot()
        {
            if (!string.IsNullOrEmpty(sCachedEditorRoot))
            {
                return sCachedEditorRoot;
            }

            try
            {
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(YokiEditorPaths).Assembly);
                if (packageInfo != null)
                {
                    sCachedEditorRoot = NormalizeAssetPath(packageInfo.assetPath + "/" + EDITOR_ROOT_RELATIVE_PATH);
                    return sCachedEditorRoot;
                }
            }
            catch
            {
                // 忽略包路径探测失败，继续用脚本定位兜底。
            }

            try
            {
                var guids = AssetDatabase.FindAssets(nameof(YokiEditorPaths) + " t:MonoScript");
                if (guids != null && guids.Length > 0)
                {
                    var scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var dir = Path.GetDirectoryName(scriptPath); // .../UISystem/Services
                    dir = Path.GetDirectoryName(dir);            // .../UISystem
                    dir = Path.GetDirectoryName(dir);            // .../Editor
                    sCachedEditorRoot = NormalizeAssetPath(dir);
                    return sCachedEditorRoot;
                }
            }
            catch
            {
                // 忽略脚本定位失败，继续使用默认路径。
            }

            sCachedEditorRoot = DEFAULT_EDITOR_ROOT;
            return sCachedEditorRoot;
        }

        public static string GetUnityRoot()
        {
            var editorRoot = GetEditorRoot();
            if (string.IsNullOrEmpty(editorRoot))
            {
                return DEFAULT_UNITY_ROOT;
            }

            var normalized = NormalizeAssetPath(editorRoot).TrimEnd('/');
            const string EDITOR_SUFFIX = "/Editor";
            if (normalized.EndsWith(EDITOR_SUFFIX))
            {
                return normalized.Substring(0, normalized.Length - EDITOR_SUFFIX.Length);
            }

            return DEFAULT_UNITY_ROOT;
        }

        public static string GetRuntimeRoot()
        {
            return NormalizeAssetPath(GetUnityRoot().TrimEnd('/') + "/Runtime");
        }

        public static string GetEditorToolsRoot()
        {
            return GetEditorRoot();
        }

        public static string GetUISystemRoot()
        {
            return NormalizeAssetPath(GetEditorRoot().TrimEnd('/') + "/UISystem");
        }

        public static string GetStylingRoot()
        {
            return NormalizeAssetPath(GetUISystemRoot().TrimEnd('/') + "/Styling");
        }

        public static string CombineWithEditorToolsRoot(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return GetEditorToolsRoot();
            return NormalizeAssetPath($"{GetEditorToolsRoot().TrimEnd('/')}/{relativePath.TrimStart('/')}");
        }

        public static string CombineWithStylingRoot(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return GetStylingRoot();
            return NormalizeAssetPath($"{GetStylingRoot().TrimEnd('/')}/{relativePath.TrimStart('/')}");
        }

        public static string CombineWithUnityRoot(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return GetUnityRoot();
            return NormalizeAssetPath($"{GetUnityRoot().TrimEnd('/')}/{relativePath.TrimStart('/')}");
        }

        public static void ClearCache()
        {
            sCachedEditorRoot = null;
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return path.Replace('\\', '/');
        }
    }
}
#endif
#endif
