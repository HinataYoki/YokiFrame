#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// 提供 UIKit Panel Prefab 创建、选中 Panel Prefab 生成和 Bind 选择操作。
    /// </summary>
    internal static class UIKitPanelPrefabService
    {
        /// <summary>创建新的 Panel Prefab、初始 UI 层级和代码文件。</summary>
        internal static string CreatePanelPrefab(string payloadJson)
        {
            UIKitPanelGenerationRequest request = UIKitPanelGenerationRequest.FromJson(payloadJson);
            UIKitPanelCodeLayout layout = new(request);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(layout.PrefabPath) != default)
                throw new InvalidOperationException("Prefab 已存在: " + layout.PrefabPath);

            UIKitPanelCodeLayout.EnsureAssetFolder(layout.PrefabFolder);
            UIKitPanelCodeLayout.EnsureAssetFolder(layout.PanelFolder);
            GameObject root = CreatePanelRoot(layout.PanelName);
            try
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, layout.PrefabPath);
                if (prefab == default) throw new InvalidOperationException("Prefab 保存失败: " + layout.PrefabPath);
                UIKitPanelGenerationResult result = GenerateForPrefab(layout, prefab);
                result.message = "UIPrefab 已创建，等待脚本编译后回填引用。";
                Selection.activeObject = prefab;
                return result.ToJson();
            }
            catch
            {
                AssetDatabase.DeleteAsset(layout.PrefabPath);
                throw;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>为当前选中的 Panel Prefab 生成绑定代码并登记编译后回填。</summary>
        internal static string GenerateCodeForSelection(string payloadJson)
        {
            UIKitPanelGenerationRequest request = UIKitPanelGenerationRequest.FromJson(payloadJson);
            ValidateSelectionContext(request, true);
            GameObject prefab = ResolveSelectedPrefab();
            RequirePanelPrefab(prefab);
            if (string.IsNullOrWhiteSpace(request.panelName)) request.panelName = prefab.name;
            request.prefabPath = AssetDatabase.GetAssetPath(prefab);
            UIKitPanelCodeLayout layout = new(request);
            UIKitPanelGenerationResult result = GenerateForPrefab(layout, prefab);
            result.message = "UI 代码已生成，等待脚本编译后回填引用。";
            return result.ToJson();
        }

        /// <summary>限制通用 selection action 只能生成 Prefab 根上的 UIPanel。</summary>
        internal static void RequirePanelPrefab(GameObject prefab)
        {
            if (prefab.GetComponent<UIPanel>() != default)
                return;
            if (prefab.GetComponent<UIComponent>() != default)
                throw new InvalidOperationException("UIComponent 请从组件 Inspector 单独生成代码。");
            if (prefab.GetComponent<UIElement>() != default)
                throw new InvalidOperationException("UIElement 请从组件 Inspector 单独生成代码。");
            throw new InvalidOperationException("Prefab 根缺少 UIPanel，无法生成 Panel 代码。");
        }

        /// <summary>为选中的 GameObject 添加兼容 Bind，并自动选择唯一确定目标组件。</summary>
        internal static string AddBindToSelection()
        {
            return AddBindToSelection("{}");
        }

        /// <summary>为选中的 GameObject 添加 Bind，并按可选 revision 防止误操作旧选择。</summary>
        /// <param name="payloadJson">可选 Selection revision 与稳定活动对象 ID。</param>
        internal static string AddBindToSelection(string payloadJson)
        {
            UIKitPanelGenerationRequest request = UIKitPanelGenerationRequest.FromJson(payloadJson);
            ValidateSelectionContext(request, false);
            GameObject[] selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
                throw new InvalidOperationException("请先选择要绑定的 UI GameObject。");
            int added = 0;
            for (var index = 0; index < selected.Length; index++)
            {
                GameObject gameObject = selected[index];
                if (gameObject == default || gameObject.GetComponent<Bind>() != default) continue;
                Bind bind = Undo.AddComponent<Bind>(gameObject);
                bind.Name = gameObject.name;
                Component target = FindPreferredTarget(gameObject);
                bind.Target = target;
                bind.AutoType = target == default ? string.Empty : target.GetType().FullName;
                bind.Type = bind.AutoType;
                EditorUtility.SetDirty(bind);
                added++;
            }

            return JsonUtility.ToJson(new UIKitPanelOperationResult
            {
                message = "已添加 Bind: " + added,
                affectedCount = added,
            });
        }

        /// <summary>从选中的 GameObject 移除 Bind，保留其它用户组件。</summary>
        internal static string RemoveBindFromSelection()
        {
            return RemoveBindFromSelection("{}");
        }

        /// <summary>从选中的 GameObject 移除 Bind，并按可选 revision 防止误操作旧选择。</summary>
        /// <param name="payloadJson">可选 Selection revision 与稳定活动对象 ID。</param>
        internal static string RemoveBindFromSelection(string payloadJson)
        {
            UIKitPanelGenerationRequest request = UIKitPanelGenerationRequest.FromJson(payloadJson);
            ValidateSelectionContext(request, false);
            GameObject[] selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
                throw new InvalidOperationException("请先选择要移除 Bind 的 UI GameObject。");
            int removed = 0;
            for (var index = 0; index < selected.Length; index++)
            {
                GameObject gameObject = selected[index];
                Bind bind = gameObject == default ? default : gameObject.GetComponent<Bind>();
                if (bind == default) continue;
                Undo.DestroyObjectImmediate(bind);
                removed++;
            }

            return JsonUtility.ToJson(new UIKitPanelOperationResult
            {
                message = "已移除 Bind: " + removed,
                affectedCount = removed,
            });
        }

        /// <summary>构建扫描树、生成源码并安排编译后的 Prefab 回填。</summary>
        internal static UIKitPanelGenerationResult GenerateForPrefab(
            UIKitPanelCodeLayout layout,
            GameObject prefab)
        {
            ValidateExistingPanelCodeOwnership(layout, prefab);
            UIKitBindScanResult scan = UIKitBindScanner.Scan(prefab);
            if (scan.HasErrors) throw CreateScanException(scan);
            System.Collections.Generic.Dictionary<string, string> sources =
                UIKitPanelCodeGenerator.BuildSources(layout, scan);
            bool scriptsChanged = UIKitPanelCodeGenerator.CommitSources(sources);
            AssetDatabase.SaveAssets();
            UIKitPendingBindingService.Queue(layout);
            if (scriptsChanged) AssetDatabase.Refresh();
            if (!scriptsChanged)
                UIKitPendingBindingService.Process();
            return new UIKitPanelGenerationResult
            {
                prefabPath = layout.PrefabPath,
                panelScriptPath = layout.PanelScriptPath,
                designerScriptPath = layout.PanelDesignerPath,
                scriptsChanged = scriptsChanged,
                bindCount = CountNodes(scan.Nodes),
                warningCount = CountWarnings(scan),
            };
        }

        /// <summary>阻止配置变化把已有用户 Panel partial 与新 Designer 生成到不同类型。</summary>
        /// <param name="layout">当前项目配置解析出的目标布局。</param>
        /// <param name="prefab">待重新生成代码的 Panel Prefab。</param>
        private static void ValidateExistingPanelCodeOwnership(
            UIKitPanelCodeLayout layout,
            GameObject prefab)
        {
            UIPanel panel = prefab.GetComponent<UIPanel>();
            if (panel == default) return;
            Type panelType = panel.GetType();
            string namespaceName = panelType.Namespace ?? string.Empty;
            string assemblyName = panelType.Assembly.GetName().Name;
            if (string.Equals(panelType.Name, layout.PanelName, StringComparison.Ordinal)
                && string.Equals(namespaceName, layout.ScriptNamespace, StringComparison.Ordinal)
                && string.Equals(assemblyName, layout.AssemblyName, StringComparison.Ordinal)) return;
            throw new InvalidOperationException(
                "Panel 已由用户脚本 " + panelType.FullName + " [" + assemblyName + "] 持有，"
                + "当前生成配置目标为 " + layout.ScriptNamespace + "." + layout.PanelName
                + " [" + layout.AssemblyName + "]。UIKit 不会自动迁移或覆盖用户 partial；"
                + "请先显式迁移现有用户脚本，或恢复原命名空间和程序集配置。");
        }

        /// <summary>创建可直接作为 UI 根的空 Panel 层级。</summary>
        private static GameObject CreatePanelRoot(string panelName)
        {
            GameObject root = new(panelName, typeof(RectTransform));
            Stretch(root.GetComponent<RectTransform>());
            GameObject panel = new("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            Stretch(panel.GetComponent<RectTransform>());
            Image image = panel.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            return root;
        }

        /// <summary>把 RectTransform 拉伸到父节点的完整区域。</summary>
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition3D = Vector3.zero;
            rect.localEulerAngles = Vector3.zero;
            rect.localScale = Vector3.one;
            rect.sizeDelta = Vector2.zero;
        }

        /// <summary>解析当前 Unity 选择对应的 Prefab 资产根。</summary>
        private static GameObject ResolveSelectedPrefab()
        {
            UnityEngine.Object selected = Selection.activeObject;
            string path = selected == default ? string.Empty : AssetDatabase.GetAssetPath(selected);
            GameObject prefab = string.IsNullOrWhiteSpace(path)
                ? default
                : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == default || PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
                throw new InvalidOperationException("当前选择不是 Prefab 资产。");
            return prefab;
        }

        /// <summary>校验 Workbench 读取上下文后 Selection 未变化且目标仍为当前活动对象。</summary>
        /// <param name="request">携带可选 revision 与稳定对象 ID 的请求。</param>
        /// <param name="requireActiveTarget">是否要求目标正是当前活动对象。</param>
        private static void ValidateSelectionContext(
            UIKitPanelGenerationRequest request,
            bool requireActiveTarget)
        {
            if (!UnityEditorContextService.MatchesRevision(request.expectedContextRevision))
            {
                throw new InvalidOperationException(
                    "Unity Editor 选择已变化，请刷新当前选择后重试。");
            }

            if (string.IsNullOrWhiteSpace(request.targetGlobalObjectId))
            {
                return;
            }

            UnityEditorContextSnapshot context = UnityEditorContextService.Capture();
            UnityEditorSelectionSnapshot selection = context.selection;
            if (!UnityEditorContextService.IsSelected(request.targetGlobalObjectId))
            {
                throw new InvalidOperationException(
                    "目标对象已不在 Unity Selection 中，请刷新当前选择后重试。");
            }

            if (requireActiveTarget
                && (selection == null
                    || !string.Equals(
                        selection.activeGlobalObjectId,
                        request.targetGlobalObjectId,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "当前活动对象与请求目标不一致，请刷新当前选择后重试。");
            }
        }

        /// <summary>严格选择组件列表中最后一个非 Bind 组件作为默认 Member 目标。</summary>
        private static Component FindPreferredTarget(GameObject gameObject)
        {
            Component[] components = gameObject.GetComponents<Component>();
            for (var index = components.Length - 1; index >= 0; index--)
            {
                Component component = components[index];
                if (component != default && component is not AbstractBind)
                    return component;
            }

            return default;
        }

        /// <summary>统计扫描树节点数量。</summary>
        private static int CountNodes(System.Collections.Generic.List<UIKitBindNode> nodes)
        {
            int count = 0;
            for (var index = 0; index < nodes.Count; index++)
                count += 1 + CountNodes(nodes[index].Children);
            return count;
        }

        /// <summary>统计扫描提示数量。</summary>
        private static int CountWarnings(UIKitBindScanResult scan)
        {
            int count = 0;
            for (var index = 0; index < scan.Diagnostics.Count; index++)
            {
                if (scan.Diagnostics[index].Severity == UIKitBindDiagnosticSeverity.Warning) count++;
            }

            return count;
        }

        /// <summary>把扫描错误转换为用户可读异常。</summary>
        private static InvalidOperationException CreateScanException(UIKitBindScanResult scan)
        {
            System.Collections.Generic.List<string> errors = new();
            for (var index = 0; index < scan.Diagnostics.Count; index++)
            {
                UIKitBindDiagnostic diagnostic = scan.Diagnostics[index];
                if (diagnostic.Severity == UIKitBindDiagnosticSeverity.Error)
                    errors.Add(diagnostic.Path + ": " + diagnostic.Message);
            }

            return new InvalidOperationException(string.Join(" | ", errors));
        }

        /// <summary>返回选择操作的简单结构化结果。</summary>
        [Serializable]
        private sealed class UIKitPanelOperationResult
        {
            public string message;
            public int affectedCount;
        }
    }
}
#endif
