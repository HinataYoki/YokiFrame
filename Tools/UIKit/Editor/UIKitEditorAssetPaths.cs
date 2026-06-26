#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace YokiFrame
{
    internal static class UIKitEditorAssetPaths
    {
        private const string PACKAGE_ROOT_FALLBACK = "Assets/YokiFrame";
        private static string sPackageRoot;

        public static string CombineWithPackageRoot(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return GetPackageRoot();

            return NormalizeAssetPath(GetPackageRoot().TrimEnd('/') + "/" + relativePath.TrimStart('/'));
        }

        public static string ResolveStyleSheetPath(string relativePath)
        {
            var path = CombineWithPackageRoot(relativePath);
#if !UNITY_2022_1_OR_NEWER
            var legacyPath = GetLegacyStyleSheetPath(path);
            if (!string.IsNullOrEmpty(legacyPath) && AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(legacyPath) != null)
                return legacyPath;
#endif
            return path;
        }

        private static string GetPackageRoot()
        {
            if (!string.IsNullOrEmpty(sPackageRoot))
                return sPackageRoot;

            try
            {
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UIKitEditorAssetPaths).Assembly);
                if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.assetPath))
                {
                    sPackageRoot = NormalizeAssetPath(packageInfo.assetPath);
                    return sPackageRoot;
                }
            }
            catch
            {
                // 包路径探测失败时继续用脚本位置兜底。
            }

            try
            {
                var guids = AssetDatabase.FindAssets(nameof(UIKitEditorAssetPaths) + " t:MonoScript");
                if (guids != null && guids.Length > 0)
                {
                    var scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var dir = Path.GetDirectoryName(scriptPath); // .../Tools/UIKit/Editor
                    dir = Path.GetDirectoryName(dir);            // .../Tools/UIKit
                    dir = Path.GetDirectoryName(dir);            // .../Tools
                    dir = Path.GetDirectoryName(dir);            // .../YokiFrame
                    sPackageRoot = NormalizeAssetPath(dir);
                    return sPackageRoot;
                }
            }
            catch
            {
                // 忽略脚本定位失败，继续使用默认路径。
            }

            sPackageRoot = PACKAGE_ROOT_FALLBACK;
            return sPackageRoot;
        }

        private static string GetLegacyStyleSheetPath(string path)
        {
            var extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension))
                return null;

            return path.Substring(0, path.Length - extension.Length) + ".Legacy" + extension;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
#endif
