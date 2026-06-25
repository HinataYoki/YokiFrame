#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// Unity Editor 侧的 UIKit 命令处理器。
    /// 运行时诊断动作仍委托给 UIKitCommandHandler，Editor-only 动作只在 Unity 编辑器程序集内实现。
    /// </summary>
    public sealed class UnityUIKitCommandHandler : IKitCommandHandler
    {
        private static readonly string[] sSupportedActions =
        {
            "stats",
            "list_panels",
            "list_stacks",
            "get_workbench_snapshot",
            "get_editor_tool_state",
            "create_panel_prefab",
            "generate_code_for_selection",
            "add_bind_to_selection",
            "remove_bind_from_selection"
        };

        private readonly UIKitCommandHandler mRuntimeHandler = new UIKitCommandHandler();

        public string KitName
        {
            get { return "UIKit"; }
        }

        public string[] SupportedActions
        {
            get { return sSupportedActions; }
        }

        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "get_editor_tool_state":
                    return BuildEditorToolStateJson();
                case "create_panel_prefab":
                    return UIKitPanelPrefabCreator.CreatePanelPrefab(payloadJson).ToJson();
                case "generate_code_for_selection":
                    return GenerateCodeForSelection(payloadJson).ToJson();
                case "add_bind_to_selection":
                    return AddBindToSelection().ToJson();
                case "remove_bind_from_selection":
                    return RemoveBindFromSelection().ToJson();
                default:
                    return mRuntimeHandler.HandleAction(action, payloadJson);
            }
        }

        private static string BuildEditorToolStateJson()
        {
            var active = Selection.activeObject;
            var activePath = active != null ? AssetDatabase.GetAssetPath(active) : string.Empty;
            var selectedCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
            var canGenerate = TryResolvePrefabForSelection(null, out _);

            return "{\"available\":true" +
                   ",\"selectedObjectCount\":" + selectedCount +
                   ",\"activeAssetPath\":\"" + JsonHelper.EscapeString(activePath) + "\"" +
                   ",\"canGenerateCode\":" + (canGenerate ? "true" : "false") +
                   ",\"defaults\":{\"prefabFolder\":\"" + JsonHelper.EscapeString(UIKitPanelPrefabCreator.DEFAULT_PREFAB_FOLDER) +
                   "\",\"scriptFolder\":\"" + JsonHelper.EscapeString(UIKitPanelPrefabCreator.DEFAULT_SCRIPT_FOLDER) +
                   "\",\"namespace\":\"" + JsonHelper.EscapeString(UIKitPanelPrefabCreator.DEFAULT_SCRIPT_NAMESPACE) +
                   "\",\"assemblyName\":\"" + JsonHelper.EscapeString(UIKitPanelPrefabCreator.DEFAULT_ASSEMBLY_NAME) +
                   "\",\"codeTemplate\":\"" + JsonHelper.EscapeString(UIKitPanelPrefabCreator.DEFAULT_CODE_TEMPLATE) + "\"}" +
                   ",\"assemblies\":" + BuildAssembliesJson() +
                   ",\"codeTemplates\":" + BuildCodeTemplatesJson() +
                   "}";
        }

        private static string BuildAssembliesJson()
        {
            var assemblies = new List<string>();
            AddUniqueAssemblyName(assemblies, UIKitPanelPrefabCreator.DEFAULT_ASSEMBLY_NAME);

            var asmdefGuids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            for (var i = 0; i < asmdefGuids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(asmdefGuids[i]);
                if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                    continue;

                AddUniqueAssemblyName(assemblies, Path.GetFileNameWithoutExtension(assetPath));
            }

            return BuildStringArrayJson(assemblies);
        }

        private static string BuildCodeTemplatesJson()
        {
            return BuildStringArrayJson(UIKitPanelPrefabCreator.GetCodeTemplateNames());
        }

        private static void AddUniqueAssemblyName(List<string> assemblies, string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return;

            for (var i = 0; i < assemblies.Count; i++)
            {
                if (string.Equals(assemblies[i], assemblyName, StringComparison.Ordinal))
                    return;
            }

            assemblies.Add(assemblyName);
        }

        private static string BuildStringArrayJson(IList<string> values)
        {
            var sb = new System.Text.StringBuilder(128);
            sb.Append('[');
            if (values != null)
            {
                for (var i = 0; i < values.Count; i++)
                {
                    if (i > 0)
                        sb.Append(',');

                    sb.Append('"');
                    sb.Append(JsonHelper.EscapeString(values[i] ?? string.Empty));
                    sb.Append('"');
                }
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static UIKitEditorCommandResult GenerateCodeForSelection(string payloadJson)
        {
            var request = UIKitPanelCreateRequest.FromJson(payloadJson);
            if (!TryResolvePrefabForSelection(request.PrefabPath, out var prefab))
            {
                throw new InvalidOperationException("请选择一个 UIPrefab，或在 payload.prefabPath 中传入 Prefab 路径。");
            }

            return UIKitPanelPrefabCreator.GenerateCodeForPrefab(prefab, request);
        }

        private static bool TryResolvePrefabForSelection(string prefabPath, out GameObject prefab)
        {
            prefab = null;
            if (!string.IsNullOrEmpty(prefabPath))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                return prefab != null && IsPrefabAsset(prefab);
            }

            var activeGameObject = Selection.activeGameObject;
            if (activeGameObject == null)
                return false;

            var assetPath = AssetDatabase.GetAssetPath(activeGameObject);
            if (!string.IsNullOrEmpty(assetPath))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                return prefab != null && IsPrefabAsset(prefab);
            }

            var source = PrefabUtility.GetCorrespondingObjectFromSource(activeGameObject);
            if (source == null)
                return false;

            assetPath = AssetDatabase.GetAssetPath(source);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            return prefab != null && IsPrefabAsset(prefab);
        }

        private static bool IsPrefabAsset(GameObject prefab)
        {
            var prefabType = PrefabUtility.GetPrefabAssetType(prefab);
            return prefabType == PrefabAssetType.Regular || prefabType == PrefabAssetType.Variant;
        }

        private static UIKitEditorCommandResult AddBindToSelection()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
                throw new InvalidOperationException("请先在 Unity Hierarchy 或 Prefab Mode 中选中 GameObject。");

            var changed = 0;
            var skipped = 0;
            for (var i = 0; i < selectedObjects.Length; i++)
            {
                var go = selectedObjects[i];
                if (go == null)
                    continue;

                if (go.GetComponent<AbstractBind>() != null)
                {
                    skipped++;
                    continue;
                }

                Undo.AddComponent<Bind>(go);
                changed++;
            }

            return UIKitEditorCommandResult.Success("已添加 Bind 组件", changed, skipped);
        }

        private static UIKitEditorCommandResult RemoveBindFromSelection()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
                throw new InvalidOperationException("请先在 Unity Hierarchy 或 Prefab Mode 中选中 GameObject。");

            var changed = 0;
            var skipped = 0;
            for (var i = 0; i < selectedObjects.Length; i++)
            {
                var go = selectedObjects[i];
                if (go == null)
                    continue;

                var bind = go.GetComponent<AbstractBind>();
                if (bind == null)
                {
                    skipped++;
                    continue;
                }

                Undo.DestroyObjectImmediate(bind);
                changed++;
            }

            return UIKitEditorCommandResult.Success("已移除 Bind 组件", changed, skipped);
        }
    }
}
#endif
