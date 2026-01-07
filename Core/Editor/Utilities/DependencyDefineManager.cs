#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace YokiFrame.Core.Editor
{
    /// <summary>
    /// 依赖检测与宏定义管理器
    /// 自动检测项目中的可选依赖，并添加对应的 Scripting Define Symbols
    /// </summary>
    [InitializeOnLoad]
    public static class DependencyDefineManager
    {
        private static readonly DependencyInfo[] sDependencies =
        {
            new("YOKIFRAME_UNITASK_SUPPORT", "com.cysharp.unitask", "UniTask"),
            new("YOKIFRAME_YOOASSET_SUPPORT", "com.tuyoogame.yooasset", "YooAsset"),
            new("YOKIFRAME_LUBAN_SUPPORT", "com.code-philosophy.luban", "Luban.Runtime"),
            new("YOKIFRAME_FMOD_SUPPORT", "com.unity.fmod", "FMODUnity"),
        };

        static DependencyDefineManager()
        {
            EditorApplication.delayCall += RefreshDefines;
            // 监听 Package Manager 变化
            UnityEditor.PackageManager.Events.registeredPackages += OnPackagesChanged;
        }

        private static void OnPackagesChanged(
            UnityEditor.PackageManager.PackageRegistrationEventArgs args)
        {
            if (args.added.Any() || args.removed.Any())
            {
                EditorApplication.delayCall += RefreshDefines;
            }
        }

        /// <summary>
        /// 监听资源导入/删除事件，自动刷新宏定义
        /// </summary>
        private class AssetPostprocessorHook : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths)
            {
                // 检测是否有 asmdef 文件变化
                var hasAsmdefChange = importedAssets.Concat(deletedAssets)
                    .Any(p => p.EndsWith(".asmdef"));
                
                if (hasAsmdefChange)
                {
                    EditorApplication.delayCall += RefreshDefines;
                }
            }
        }

        [MenuItem("YokiFrame/刷新依赖宏定义")]
        public static void RefreshDefines()
        {
            var currentDefines = GetCurrentDefines();
            var newDefines = new HashSet<string>(currentDefines);
            var changed = false;

            foreach (var dep in sDependencies)
            {
                var exists = DetectDependency(dep);
                
                if (exists && !newDefines.Contains(dep.Define))
                {
                    newDefines.Add(dep.Define);
                    changed = true;
                }
                else if (!exists && newDefines.Contains(dep.Define))
                {
                    newDefines.Remove(dep.Define);
                    changed = true;
                }
            }

            if (changed)
            {
                SetDefines(newDefines);
                Debug.Log("[YokiFrame] 依赖宏定义已更新");
            }
        }

        private static bool DetectDependency(DependencyInfo dep)
        {
            // 方式1：检测 Package 是否存在
            if (!string.IsNullOrEmpty(dep.PackageName))
            {
                var packagePath = $"Packages/{dep.PackageName}";
                if (Directory.Exists(packagePath)) return true;
            }

            // 方式2：检测 asmdef 是否存在
            if (!string.IsNullOrEmpty(dep.AsmdefName))
            {
                var guids = AssetDatabase.FindAssets($"t:AssemblyDefinitionAsset {dep.AsmdefName}");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(path) == dep.AsmdefName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static HashSet<string> GetCurrentDefines()
        {
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            PlayerSettings.GetScriptingDefineSymbols(target, out var defines);
            return new HashSet<string>(defines);
        }

        private static void SetDefines(HashSet<string> defines)
        {
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            PlayerSettings.SetScriptingDefineSymbols(target, defines.ToArray());
        }

        private readonly struct DependencyInfo
        {
            public readonly string Define;
            public readonly string PackageName;
            public readonly string AsmdefName;

            public DependencyInfo(string define, string packageName, string asmdefName)
            {
                Define = define;
                PackageName = packageName;
                AsmdefName = asmdefName;
            }
        }
    }
}
#endif
