#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>创建 Workbench Editor Tools 使用的当前 Unity 选择上下文。</summary>
    internal static class UIKitEditorContextWriter
    {
        private const string DEFAULT_ASSEMBLY_NAME = "Assembly-CSharp";

        /// <summary>读取当前选择和稳定生成默认值，不修改任何 Unity 对象。</summary>
        internal static string Write()
        {
            UnityEditorContextSnapshot commonContext = UnityEditorContextService.Capture();
            UnityEditorSelectionSnapshot selection = commonContext.selection;
            UnityEditorObjectSnapshot selected = selection == null ? default : selection.activeObject;
            string selectedPath = selected == null ? string.Empty : selected.assetPath;
            GameObject prefab = string.IsNullOrWhiteSpace(selectedPath)
                ? default
                : AssetDatabase.LoadAssetAtPath<GameObject>(selectedPath);
            int bindCount = CountSelectedBinds();
            UIKitPanelGenerationRequest defaults = UIKitPanelGenerationRequest.CreateDefault();
            UIKitEditorContext context = new()
            {
                available = true,
                contextRevision = commonContext.revision,
                selectedAssetPath = selectedPath,
                selectedObjectName = selected == null ? string.Empty : selected.name,
                activeGlobalObjectId = selected == null ? string.Empty : selected.globalObjectId,
                selectedGameObjectCount = Selection.gameObjects == null ? 0 : Selection.gameObjects.Length,
                selectedBindCount = bindCount,
                canGenerateCode = prefab != default
                    && PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.NotAPrefab,
                canAddBind = Selection.gameObjects != null && Selection.gameObjects.Length > 0,
                canRemoveBind = bindCount > 0,
                prefabFolder = defaults.prefabFolder,
                scriptFolder = defaults.scriptFolder,
                scriptNamespace = defaults.scriptNamespace,
                assemblyName = defaults.assemblyName,
                codeTemplate = defaults.codeTemplate,
                codeTemplateOptions = GetCodeTemplateOptions(),
                assemblyNames = GetAssemblyNames(),
                scenePath = commonContext.scene == null ? string.Empty : commonContext.scene.path,
                sceneName = commonContext.scene == null ? string.Empty : commonContext.scene.name,
                prefabStageActive = commonContext.prefabStage != null && commonContext.prefabStage.active,
                prefabStageAssetPath = commonContext.prefabStage == null
                    ? string.Empty
                    : commonContext.prefabStage.assetPath,
                editorMode = commonContext.editor == null ? string.Empty : commonContext.editor.mode,
            };
            return JsonUtility.ToJson(context);
        }

        /// <summary>统计当前选择中携带 Bind 的 GameObject 数量。</summary>
        private static int CountSelectedBinds()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected == null) return 0;
            int count = 0;
            for (var index = 0; index < selected.Length; index++)
            {
                if (selected[index] != default && selected[index].GetComponent<Bind>() != default) count++;
            }

            return count;
        }

        /// <summary>复制当前 Editor Registry 的模板名，供 Workbench 选择器动态展示。</summary>
        /// <returns>稳定模板名数组。</returns>
        private static string[] GetCodeTemplateOptions()
        {
            IReadOnlyList<string> names = UIKitCodeTemplateRegistry.GetTemplateNames();
            string[] options = new string[names.Count];
            for (var index = 0; index < names.Count; index++) options[index] = names[index];
            return options;
        }

        /// <summary>扫描当前项目可承载生成脚本的 Player 程序集，并固定默认程序集排在首位。</summary>
        /// <returns>稳定去重后的程序集名称数组。</returns>
        private static string[] GetAssemblyNames()
        {
            HashSet<string> names = new(StringComparer.Ordinal) { DEFAULT_ASSEMBLY_NAME };
            UnityEditor.Compilation.Assembly[] assemblies =
                CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            for (var index = 0; index < assemblies.Length; index++)
            {
                UnityEditor.Compilation.Assembly assembly = assemblies[index];
                if (!IsProjectPlayerAssembly(assembly)) continue;
                names.Add(assembly.name);
            }

            string[] result = new string[names.Count];
            names.CopyTo(result);
            Array.Sort(result, CompareAssemblyNames);
            return result;
        }

        /// <summary>只保留源文件位于当前项目 Assets 的非测试 Player 编译单元。</summary>
        /// <param name="assembly">CompilationPipeline 返回的候选程序集。</param>
        /// <returns>该程序集可作为项目生成代码目标时返回 true。</returns>
        private static bool IsProjectPlayerAssembly(UnityEditor.Compilation.Assembly assembly)
        {
            if (string.IsNullOrWhiteSpace(assembly.name)) return false;
            string[] sourceFiles = assembly.sourceFiles;
            if (sourceFiles == null) return false;
            for (var index = 0; index < sourceFiles.Length; index++)
            {
                if (IsProjectAssetPath(sourceFiles[index])) return true;
            }

            return false;
        }

        /// <summary>兼容相对与绝对 source path，判断源码是否属于当前 Unity 项目的 Assets 目录。</summary>
        /// <param name="sourcePath">CompilationPipeline 提供的源码路径。</param>
        /// <returns>路径属于项目 Assets 时返回 true。</returns>
        private static bool IsProjectAssetPath(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) return false;
            string normalizedSourcePath = sourcePath.Replace('\\', '/');
            if (normalizedSourcePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return true;
            string normalizedDataPath = Application.dataPath.Replace('\\', '/').TrimEnd('/');
            return normalizedSourcePath.StartsWith(
                normalizedDataPath + "/",
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>确保默认程序集保持首位，其余候选使用 ordinal 顺序稳定排列。</summary>
        /// <param name="left">待比较的左侧程序集名。</param>
        /// <param name="right">待比较的右侧程序集名。</param>
        /// <returns>排序比较结果。</returns>
        private static int CompareAssemblyNames(string left, string right)
        {
            bool leftIsDefault = string.Equals(left, DEFAULT_ASSEMBLY_NAME, StringComparison.Ordinal);
            bool rightIsDefault = string.Equals(right, DEFAULT_ASSEMBLY_NAME, StringComparison.Ordinal);
            if (leftIsDefault != rightIsDefault) return leftIsDefault ? -1 : 1;
            return string.Compare(left, right, StringComparison.Ordinal);
        }

        /// <summary>JsonUtility 使用的 Editor context DTO。</summary>
        [Serializable]
        private sealed class UIKitEditorContext
        {
            public bool available;
            public long contextRevision;
            public string selectedAssetPath;
            public string selectedObjectName;
            public string activeGlobalObjectId;
            public int selectedGameObjectCount;
            public int selectedBindCount;
            public bool canGenerateCode;
            public bool canAddBind;
            public bool canRemoveBind;
            public string prefabFolder;
            public string scriptFolder;
            public string scriptNamespace;
            public string assemblyName;
            public string codeTemplate;
            public string[] codeTemplateOptions;
            public string[] assemblyNames;
            public string scenePath;
            public string sceneName;
            public bool prefabStageActive;
            public string prefabStageAssetPath;
            public string editorMode;
        }
    }
}
#endif
