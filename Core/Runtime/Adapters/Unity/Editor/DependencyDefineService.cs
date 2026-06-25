#if !GODOT
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity 侧可选依赖宏定义服务，负责把包环境同步到 YokiFrame 编译宏。
    /// </summary>
    [InitializeOnLoad]
    public static class DependencyDefineService
    {
        /// <summary>
        /// UniTask 可选依赖编译宏。
        /// </summary>
        public const string UNITASK_SUPPORT_DEFINE = "YOKIFRAME_UNITASK_SUPPORT";

        /// <summary>
        /// YooAsset 可选依赖编译宏。
        /// </summary>
        public const string YOOASSET_SUPPORT_DEFINE = "YOKIFRAME_YOOASSET_SUPPORT";

        /// <summary>
        /// Luban 可选依赖编译宏。
        /// </summary>
        public const string LUBAN_SUPPORT_DEFINE = "YOKIFRAME_LUBAN_SUPPORT";

        /// <summary>
        /// ZString 可选依赖编译宏。
        /// </summary>
        public const string ZSTRING_SUPPORT_DEFINE = "YOKIFRAME_ZSTRING_SUPPORT";

        /// <summary>
        /// DOTween 可选依赖编译宏。
        /// </summary>
        public const string DOTWEEN_SUPPORT_DEFINE = "YOKIFRAME_DOTWEEN_SUPPORT";

        /// <summary>
        /// Input System 可选依赖编译宏。
        /// </summary>
        public const string INPUTSYSTEM_SUPPORT_DEFINE = "YOKIFRAME_INPUTSYSTEM_SUPPORT";

        /// <summary>
        /// FMOD 可选依赖编译宏。
        /// </summary>
        public const string FMOD_SUPPORT_DEFINE = "YOKIFRAME_FMOD_SUPPORT";

        /// <summary>
        /// Nino 可选依赖编译宏。
        /// </summary>
        public const string NINO_SUPPORT_DEFINE = "YOKIFRAME_NINO_SUPPORT";

        /// <summary>
        /// UniTask 可选依赖编译宏。
        /// </summary>
        public static string UniTaskSupportDefine => UNITASK_SUPPORT_DEFINE;

        /// <summary>
        /// YooAsset 可选依赖编译宏。
        /// </summary>
        public static string YooAssetSupportDefine => YOOASSET_SUPPORT_DEFINE;

        /// <summary>
        /// Luban 可选依赖编译宏。
        /// </summary>
        public static string LubanSupportDefine => LUBAN_SUPPORT_DEFINE;

        /// <summary>
        /// ZString 可选依赖编译宏。
        /// </summary>
        public static string ZStringSupportDefine => ZSTRING_SUPPORT_DEFINE;

        /// <summary>
        /// DOTween 可选依赖编译宏。
        /// </summary>
        public static string DOTweenSupportDefine => DOTWEEN_SUPPORT_DEFINE;

        /// <summary>
        /// Input System 可选依赖编译宏。
        /// </summary>
        public static string InputSystemSupportDefine => INPUTSYSTEM_SUPPORT_DEFINE;

        /// <summary>
        /// FMOD 可选依赖编译宏。
        /// </summary>
        public static string FMODSupportDefine => FMOD_SUPPORT_DEFINE;

        /// <summary>
        /// Nino 可选依赖编译宏。
        /// </summary>
        public static string NinoSupportDefine => NINO_SUPPORT_DEFINE;

        private static readonly DependencyInfo[] sDependencies =
        {
            new DependencyInfo(UNITASK_SUPPORT_DEFINE, "com.cysharp.unitask", "UniTask", "Cysharp.Threading.Tasks.UniTask"),
            new DependencyInfo(YOOASSET_SUPPORT_DEFINE, "com.tuyoogame.yooasset", "YooAsset", "YooAsset.YooAssets"),
            new DependencyInfo(LUBAN_SUPPORT_DEFINE, "com.code-philosophy.luban", "Luban.Runtime", "Luban.ByteBuf"),
            new DependencyInfo(ZSTRING_SUPPORT_DEFINE, "com.cysharp.zstring", "ZString", "Cysharp.Text.ZString"),
            new DependencyInfo(DOTWEEN_SUPPORT_DEFINE, "com.demigiant.dotween", "DOTween.Modules", "DG.Tweening.DOTween"),
            new DependencyInfo(INPUTSYSTEM_SUPPORT_DEFINE, "com.unity.inputsystem", "Unity.InputSystem", "UnityEngine.InputSystem.InputAction"),
            new DependencyInfo(FMOD_SUPPORT_DEFINE, "com.unity.fmod", "FMODUnity", "FMODUnity.RuntimeManager"),
            new DependencyInfo(NINO_SUPPORT_DEFINE, "com.jasonxudeveloper.nino", null, "Nino.Core.NinoTypeAttribute"),
        };

        static DependencyDefineService()
        {
            EditorApplication.delayCall += RefreshDefines;
            UnityEditor.PackageManager.Events.registeredPackages += OnPackagesChanged;
        }

        [MenuItem("YokiFrame/Refresh Dependency Defines")]
        /// <summary>
        /// 刷新当前 Unity 构建目标的 YokiFrame 可选依赖编译宏。
        /// </summary>
        public static void RefreshDefines()
        {
            var currentDefines = GetCurrentDefines();
            var newDefines = new HashSet<string>(currentDefines);

            for (var i = 0; i < sDependencies.Length; i++)
            {
                var dependency = sDependencies[i];
                var exists = DetectDependency(dependency);
                if (exists)
                    newDefines.Add(dependency.Define);
                else
                    newDefines.Remove(dependency.Define);
            }

            if (newDefines.Count != currentDefines.Count || !newDefines.SetEquals(currentDefines))
                SetDefines(newDefines);
        }

        private static void OnPackagesChanged(UnityEditor.PackageManager.PackageRegistrationEventArgs args)
        {
            if (HasAnyElement(args.added) || HasAnyElement(args.removed))
                EditorApplication.delayCall += RefreshDefines;
        }

        private static bool HasAnyElement<T>(IEnumerable<T> collection)
        {
            foreach (var _ in collection)
                return true;

            return false;
        }

        private sealed class AssetPostprocessorHook : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths)
            {
                if (HasAsmdefFile(importedAssets) || HasAsmdefFile(deletedAssets) ||
                    HasAsmdefFile(movedAssets) || HasAsmdefFile(movedFromAssetPaths))
                {
                    EditorApplication.delayCall += RefreshDefines;
                }
            }
        }

        private static bool HasAsmdefFile(string[] assets)
        {
            if (assets == null)
                return false;

            for (var i = 0; i < assets.Length; i++)
            {
                if (!string.IsNullOrEmpty(assets[i]) && assets[i].EndsWith(".asmdef"))
                    return true;
            }

            return false;
        }

        private static bool DetectDependency(DependencyInfo dependency)
        {
            try
            {
                if (!string.IsNullOrEmpty(dependency.PackageName) && HasPackage(dependency.PackageName))
                    return true;

                if (!string.IsNullOrEmpty(dependency.AsmdefName) && HasAsmdef(dependency.AsmdefName))
                    return true;

                if (!string.IsNullOrEmpty(dependency.TypeName) && HasLoadedTypeWithExistingAssembly(dependency.TypeName))
                    return true;
            }
            catch (System.Exception e)
            {
                LogKit.Warning("[YokiFrame][DependencyDefineService] 检测依赖失败: " + dependency.Define + ", " + e.Message);
            }

            return false;
        }

        private static bool HasPackage(string packageName)
        {
            var request = UnityEditor.PackageManager.Client.List(true, false);
            while (!request.IsCompleted)
            {
            }

            if (request.Status != UnityEditor.PackageManager.StatusCode.Success)
                return false;

            foreach (var package in request.Result)
            {
                if (package.name == packageName)
                    return true;
            }

            return false;
        }

        private static bool HasAsmdef(string asmdefName)
        {
            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (Path.GetFileNameWithoutExtension(path) == asmdefName)
                    return true;
            }

            return false;
        }

        private static bool HasLoadedTypeWithExistingAssembly(string typeName)
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                if (assembly == null || assembly.IsDynamic)
                    continue;

                var type = assembly.GetType(typeName);
                if (type == null)
                    continue;

                try
                {
                    if (!string.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location))
                        return true;
                }
                catch
                {
                    return true;
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
            var defineArray = new string[defines.Count];
            defines.CopyTo(defineArray);
            PlayerSettings.SetScriptingDefineSymbols(target, defineArray);
        }

        private readonly struct DependencyInfo
        {
            public readonly string Define;
            public readonly string PackageName;
            public readonly string AsmdefName;
            public readonly string TypeName;

            public DependencyInfo(string define, string packageName, string asmdefName, string typeName)
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
