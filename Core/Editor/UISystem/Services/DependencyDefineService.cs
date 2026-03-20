#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 依赖检测与宏定义服务
    /// 自动检测项目中的可选依赖，并添加对应的 Scripting Define Symbols
    /// </summary>
    /// <remarks>
    /// <para>支持的依赖检测方式：</para>
    /// <list type="bullet">
    /// <item>Package Manager 包检测（通过 package.json）</item>
    /// <item>Assembly Definition 检测（通过 .asmdef 文件）</item>
    /// <item>Type 检测（通过反射检测类型是否存在，适用于 precompiled DLL）</item>
    /// </list>
    /// <para>自动触发时机：</para>
    /// <list type="bullet">
    /// <item>编辑器启动时（InitializeOnLoad）</item>
    /// <item>Package Manager 包变化时</item>
    /// <item>.asmdef 文件导入/删除时</item>
    /// </list>
    /// <para>手动触发：菜单 YokiFrame/刷新依赖宏定义</para>
    /// </remarks>
    [InitializeOnLoad]
    public static class DependencyDefineService
    {
        private static readonly DependencyInfo[] sDependencies =
        {
            new("YOKIFRAME_UNITASK_SUPPORT", "com.cysharp.unitask", "UniTask"),
            new("YOKIFRAME_YOOASSET_SUPPORT", "com.tuyoogame.yooasset", "YooAsset"),
            new("YOKIFRAME_LUBAN_SUPPORT", "com.code-philosophy.luban", "Luban.Runtime"),
            new("YOKIFRAME_FMOD_SUPPORT", "com.unity.fmod", "FMODUnity"),
            new("YOKIFRAME_DOTWEEN_SUPPORT", "com.demigiant.dotween", "DOTween.Modules"),
            new("YOKIFRAME_INPUTSYSTEM_SUPPORT", "com.unity.inputsystem", "Unity.InputSystem"),
            new("YOKIFRAME_ZSTRING_SUPPORT", "com.cysharp.zstring", "ZString"),
            new("YOKIFRAME_NINO_SUPPORT", "com.jasonxudeveloper.nino", null, "Nino.Core.NinoTypeAttribute"),
        };

        static DependencyDefineService()
        {
            EditorApplication.delayCall += RefreshDefines;
            // 监听 Package Manager 变化
            UnityEditor.PackageManager.Events.registeredPackages += OnPackagesChanged;
        }

        private static void OnPackagesChanged(
            UnityEditor.PackageManager.PackageRegistrationEventArgs args)
        {
            bool hasChanges = HasAnyElement(args.added) || HasAnyElement(args.removed);
            if (hasChanges)
            {
                EditorApplication.delayCall += RefreshDefines;
            }
        }

        /// <summary>
        /// 检查集合是否包含任何元素（避免 LINQ Any()）
        /// </summary>
        private static bool HasAnyElement<T>(System.Collections.Generic.IEnumerable<T> collection)
        {
            foreach (var _ in collection)
            {
                return true;
            }
            return false;
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
                bool hasAsmdefChange = HasAsmdefFile(importedAssets) || HasAsmdefFile(deletedAssets);
                if (hasAsmdefChange)
                {
                    EditorApplication.delayCall += RefreshDefines;
                }
            }
        }

        /// <summary>
        /// 检查资源数组中是否包含 .asmdef 文件
        /// </summary>
        private static bool HasAsmdefFile(string[] assets)
        {
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i].EndsWith(".asmdef"))
                {
                    return true;
                }
            }
            return false;
        }

        [MenuItem("YokiFrame/刷新依赖宏定义")]
        public static void RefreshDefines()
        {
            var currentDefines = GetCurrentDefines();
            var newDefines = new HashSet<string>(currentDefines);

            foreach (var dep in sDependencies)
            {
                var exists = DetectDependency(dep);

                if (exists && !newDefines.Contains(dep.Define))
                {
                    newDefines.Add(dep.Define);
                    Debug.Log($"[YokiFrame] 添加宏定义: {dep.Define}");
                }
                else if (!exists && newDefines.Contains(dep.Define))
                {
                    newDefines.Remove(dep.Define);
                    Debug.Log($"[YokiFrame] 移除宏定义: {dep.Define}");
                }
            }

            if (newDefines.Count != currentDefines.Count || !newDefines.SetEquals(currentDefines))
            {
                SetDefines(newDefines);
            }
        }

        private static bool DetectDependency(DependencyInfo dep)
        {
            try
            {
                // 方式1：通过 PackageInfo API 检测包是否存在（支持 registry / git / local 等所有安装方式）
                if (!string.IsNullOrEmpty(dep.PackageName))
                {
                    var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForPackageName(dep.PackageName);
                    if (packageInfo != null) return true;
                }

                // 方式2：检测 asmdef 是否存在
                if (!string.IsNullOrEmpty(dep.AsmdefName))
                {
                    // 搜索所有 asmdef 文件
                    var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
                    foreach (var guid in guids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var fileName = Path.GetFileNameWithoutExtension(path);
                        if (fileName == dep.AsmdefName)
                        {
                            return true;
                        }
                    }
                }

                // 方式3：检测类型所在的 DLL 是否存在于磁盘（适用于 precompiled DLL 无 asmdef 的包）
                // 注意：不能仅用 AppDomain.GetAssemblies() 检测，因为 DLL 移除后已加载的 assembly 仍在内存中
                if (!string.IsNullOrEmpty(dep.TypeName))
                {
                    var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var assembly in assemblies)
                    {
                        if (assembly.IsDynamic) continue;
                        var type = assembly.GetType(dep.TypeName);
                        if (type != null)
                        {
                            // 类型在内存中找到，但需确认 DLL 文件仍存在于磁盘
                            try
                            {
                                if (File.Exists(assembly.Location))
                                    return true;
                            }
                            catch { /* Location 可能为空或无效 */ }
                        }
                    }
                }

                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DependencyDefineService] 检测依赖失败: {dep.Define}, 错误: {ex.Message}");
                return false;
            }
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
            // 使用数组替代 LINQ ToArray()
            var definesArray = new string[defines.Count];
            defines.CopyTo(definesArray);
            PlayerSettings.SetScriptingDefineSymbols(target, definesArray);
        }

        private readonly struct DependencyInfo
        {
            public readonly string Define;
            public readonly string PackageName;
            public readonly string AsmdefName;
            public readonly string TypeName;

            public DependencyInfo(string define, string packageName, string asmdefName, string typeName = null)
            {
                Define = define;
                PackageName = packageName;
                AsmdefName = asmdefName;
                TypeName = typeName;
            }
        }
    }
}
#endif
