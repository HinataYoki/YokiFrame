#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace YokiFrame.EditorTools
{
    public static class YokiEditorPaths
    {
        private static string sCachedEditorToolsRoot;

        public static string GetEditorToolsRoot()
        {
            if (!string.IsNullOrEmpty(sCachedEditorToolsRoot))
            {
                return sCachedEditorToolsRoot;
            }

            // 1) Prefer UPM package location
            try
            {
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(YokiEditorPaths).Assembly);
                if (packageInfo != null)
                {
                    // packageInfo.assetPath is like "Packages/com.xxx.yokiframe"
                    sCachedEditorToolsRoot = NormalizeAssetPath($"{packageInfo.assetPath}/Core/Editor/YokiEditorTools");
                    return sCachedEditorToolsRoot;
                }
            }
            catch
            {
                // Ignore and fallback
            }

            // 2) Fallback: locate this script by GUID and walk up to .../Core/Editor/YokiEditorTools
            try
            {
                var guids = AssetDatabase.FindAssets($"{nameof(YokiEditorPaths)} t:MonoScript");
                if (guids != null && guids.Length > 0)
                {
                    var scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var dir = Path.GetDirectoryName(scriptPath); // .../YokiEditorTools/Services
                    dir = Path.GetDirectoryName(dir);            // .../YokiEditorTools
                    sCachedEditorToolsRoot = NormalizeAssetPath(dir);
                    return sCachedEditorToolsRoot;
                }
            }
            catch
            {
                // Ignore and fallback
            }

            // 3) Last resort: keep old default
            sCachedEditorToolsRoot = "Assets/YokiFrame/Core/Editor/YokiEditorTools";
            return sCachedEditorToolsRoot;
        }

        public static string GetStylingRoot()
        {
            return NormalizeAssetPath($"{GetEditorToolsRoot()}/Styling");
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

        public static void ClearCache()
        {
            sCachedEditorToolsRoot = null;
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return path.Replace('\\', '/');
        }
    }
}
#endif
