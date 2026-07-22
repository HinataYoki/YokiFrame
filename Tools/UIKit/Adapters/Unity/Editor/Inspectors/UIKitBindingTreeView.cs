#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.Unity.Inspector;

namespace YokiFrame
{
    /// <summary>为 UIPanel、UIElement 和 UIComponent 提供统一的 InspectorKit 绑定树。</summary>
    internal sealed partial class UIKitBindingTreeView
    {
        private const string COLLAPSED_PATHS_PREFIX = "YokiFrame.InspectorKit.UIKit.BindingTree.";

        private readonly Func<Component> mOwnerProvider;
        private readonly Action mOpenScript;
        private readonly Action mGenerateCode;
        private readonly string mCardStateKey;
        private readonly string mGenerateLabel;
        private readonly UIKitGeneratedOwnerKind? mOwnerKind;
        private readonly HashSet<string> mCollapsedPaths = new(StringComparer.Ordinal);
        private VisualElement mTreeView;
        private Label mStatsLabel;
        private VisualElement mValidation;

        /// <summary>创建绑定树视图，并绑定当前 owner 与两个明确操作。</summary>
        internal UIKitBindingTreeView(
            Func<Component> ownerProvider,
            Action openScript,
            Action generateCode,
            string cardStateKey,
            string generateLabel,
            UIKitGeneratedOwnerKind? ownerKind = null)
        {
            mOwnerProvider = ownerProvider ?? throw new ArgumentNullException(nameof(ownerProvider));
            mOpenScript = openScript ?? throw new ArgumentNullException(nameof(openScript));
            mGenerateCode = generateCode ?? throw new ArgumentNullException(nameof(generateCode));
            mCardStateKey = cardStateKey ?? string.Empty;
            mGenerateLabel = generateLabel ?? "生成 UI 代码";
            mOwnerKind = ownerKind;
            LoadCollapsedPaths();
        }

        /// <summary>创建操作栏、层级、图例、统计与扫描诊断。</summary>
        internal VisualElement Create()
        {
            return InspectorKitUi.CreateCard(
                "绑定树",
                mCardStateKey,
                InspectorCardInitialState.Expanded,
                body =>
                {
                    body.Add(CreateActions());
                    mTreeView = InspectorKitUi.CreateHierarchyView();
                    body.Add(mTreeView);
                    body.Add(CreateLegend());
                    mStatsLabel = new Label();
                    mStatsLabel.AddToClassList("yoki-editor-inspector__secondary-text");
                    body.Add(mStatsLabel);
                    mValidation = new VisualElement();
                    body.Add(mValidation);
                    Refresh();
                });
        }

        /// <summary>重新扫描当前 owner，并刷新可见节点、统计与诊断。</summary>
        internal void Refresh()
        {
            if (mTreeView == null)
                return;
            mTreeView.Clear();
            Component owner = mOwnerProvider();
            if (owner == default)
                return;
            InspectorBindingNode root = Collect(owner.gameObject);
            BindingTreeStats stats = new();
            int renderedCount = RenderChildren(root, 0, stats);
            if (renderedCount == 0)
            {
                Label empty = new("未找到任何绑定信息");
                empty.AddToClassList("yoki-editor-inspector__hierarchy-empty");
                mTreeView.Add(empty);
            }
            RefreshStats(stats);
            RefreshValidation(owner, renderedCount);
        }

        /// <summary>创建打开脚本、刷新和 owner 专有生成按钮。</summary>
        private VisualElement CreateActions()
        {
            Button open = InspectorKitUi.CreateActionButton("打开脚本", mOpenScript);
            Button refresh = InspectorKitUi.CreateActionButton("刷新绑定树", Refresh);
            Button generate = InspectorKitUi.CreateActionButton(
                mGenerateLabel,
                mGenerateCode,
                InspectorActionStyle.Primary);
            VisualElement row = InspectorKitUi.CreateCompactButtonRow(open, refresh, generate);
            row.style.justifyContent = Justify.FlexEnd;
            return row;
        }

        /// <summary>创建四种 BindType 的统一图例。</summary>
        private static VisualElement CreateLegend()
        {
            return InspectorKitUi.CreateHierarchyLegend(
                InspectorKitUi.CreateHierarchyLegendItem(
                    GetMarker(BindType.Member), "Member", GetColor(BindType.Member)),
                InspectorKitUi.CreateHierarchyLegendItem(
                    GetMarker(BindType.Element), "Element", GetColor(BindType.Element)),
                InspectorKitUi.CreateHierarchyLegendItem(
                    GetMarker(BindType.Component), "Component", GetColor(BindType.Component)),
                InspectorKitUi.CreateHierarchyLegendItem(
                    GetMarker(BindType.Leaf), "Leaf", GetColor(BindType.Leaf)));
        }

        /// <summary>收集与生成器 owner 作用域一致的只读绑定树。</summary>
        private static InspectorBindingNode Collect(GameObject root)
        {
            InspectorBindingNode rootNode = new(root.name, root.name, root.name, default, root);
            CollectChildren(root.transform, rootNode, root.name);
            return rootNode;
        }

        /// <summary>递归收集 Bind，并把非容器节点的后代展平到当前 owner。</summary>
        private static void CollectChildren(
            Transform parent,
            InspectorBindingNode owner,
            string parentPath)
        {
            for (var index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                string path = parentPath + "/" + child.name;
                AbstractBind bind = child.GetComponent<AbstractBind>();
                if (bind == default)
                {
                    CollectChildren(child, owner, path);
                    continue;
                }
                if (TryAddMemberTargets(bind, owner, path))
                {
                    CollectChildren(child, owner, path);
                    continue;
                }
                InspectorBindingNode node = CreateNode(bind, path);
                owner.Children.Add(node);
                if (bind.Bind == BindType.Leaf)
                    continue;
                InspectorBindingNode nextOwner = CanContainChildren(bind) ? node : owner;
                CollectChildren(child, nextOwner, path);
            }
        }

        /// <summary>把内置 Member 多目标展开为多个展示节点。</summary>
        private static bool TryAddMemberTargets(
            AbstractBind bind,
            InspectorBindingNode owner,
            string path)
        {
            if (bind.MemberTargets.Count == 0
                || !UIKitBindStrategyRegistry.TryGet(bind, out IUIKitBindStrategy strategy, out _)
                || strategy.OutputKind != UIKitBindOutputKind.Member)
                return false;
            for (var index = 0; index < bind.MemberTargets.Count; index++)
                AddMemberTargetNode(bind, owner, path, index);
            return true;
        }

        /// <summary>向展示树追加一个多 Member 目标节点。</summary>
        private static void AddMemberTargetNode(
            AbstractBind bind,
            InspectorBindingNode owner,
            string path,
            int index)
        {
            BindMemberTarget item = bind.MemberTargets[index];
            Component target = item == null ? default : item.Target;
            string fieldName = item == null || string.IsNullOrWhiteSpace(item.Name)
                ? UIKitBindMemberNaming.CreateDefaultName(bind, target, bind.MemberTargets, index)
                : item.Name.Trim();
            string typeName = target == default ? string.Empty : target.GetType().FullName;
            owner.Children.Add(new InspectorBindingNode(
                fieldName,
                typeName,
                path + "[" + index + "]",
                BindType.Member,
                bind.gameObject));
        }

        /// <summary>把普通 Bind 解析为展示节点，失败时保留兼容字段。</summary>
        private static InspectorBindingNode CreateNode(AbstractBind bind, string path)
        {
            string typeName = bind.Type;
            if (UIKitBindStrategyRegistry.TryGet(bind, out IUIKitBindStrategy strategy, out _)
                && strategy.TryResolve(bind, out string resolvedType, out _, out _))
                typeName = resolvedType;
            string fieldName = string.IsNullOrWhiteSpace(bind.Name)
                ? bind.gameObject.name
                : bind.Name.Trim();
            return new InspectorBindingNode(
                fieldName,
                typeName,
                path,
                bind.Bind,
                bind.gameObject);
        }

        /// <summary>判断当前 Bind 是否建立子 owner 作用域。</summary>
        private static bool CanContainChildren(AbstractBind bind)
        {
            return UIKitBindStrategyRegistry.TryGet(bind, out IUIKitBindStrategy strategy, out _)
                && strategy.CanContainChildren;
        }

        /// <summary>按树顺序渲染未折叠的绑定节点。</summary>
        private int RenderChildren(InspectorBindingNode parent, int depth, BindingTreeStats stats)
        {
            int rendered = 0;
            for (var index = 0; index < parent.Children.Count; index++)
            {
                InspectorBindingNode node = parent.Children[index];
                bool hasChildren = node.Children.Count > 0;
                bool expanded = !mCollapsedPaths.Contains(node.Path);
                mTreeView.Add(CreateItem(node, depth, hasChildren, expanded));
                stats.Add(node.BindType);
                rendered++;
                if (hasChildren && expanded)
                    rendered += RenderChildren(node, depth + 1, stats);
            }
            return rendered;
        }

        /// <summary>创建可以定位对象和切换折叠的单个层级项。</summary>
        private VisualElement CreateItem(
            InspectorBindingNode node,
            int depth,
            bool hasChildren,
            bool expanded)
        {
            return InspectorKitUi.CreateHierarchyItem(
                depth,
                GetMarker(node.BindType),
                node.Name,
                ShortTypeName(node.TypeName),
                node.BindType.ToString(),
                GetColor(node.BindType),
                hasChildren,
                expanded,
                () => TogglePath(node.Path),
                () => SelectObject(node.GameObject));
        }

        /// <summary>切换一个节点折叠状态并持久化。</summary>
        private void TogglePath(string path)
        {
            if (!mCollapsedPaths.Add(path))
                mCollapsedPaths.Remove(path);
            SaveCollapsedPaths();
            Refresh();
        }

        /// <summary>在 Hierarchy 或 Project 视图定位绑定对象。</summary>
        private static void SelectObject(GameObject gameObject)
        {
            if (gameObject == default)
                return;
            Selection.activeGameObject = gameObject;
            EditorGUIUtility.PingObject(gameObject);
        }

        /// <summary>刷新四种绑定类型的数量摘要。</summary>
        private void RefreshStats(BindingTreeStats stats)
        {
            mStatsLabel.text = "共 " + stats.Total + " 个绑定（"
                + stats.Member + " Member, "
                + stats.Element + " Element, "
                + stats.Component + " Component, "
                + stats.Leaf + " Leaf）";
        }

        /// <summary>显示 Panel 公共校验或 Element/Component 绑定扫描结果。</summary>
        private void RefreshValidation(Component owner, int renderedCount)
        {
            InspectorKitUi.Refresh(mValidation, body =>
            {
                if (owner is UIPanel)
                {
                    AddPanelValidation(body, UIPanelValidator.ValidatePanel(owner.gameObject));
                    return;
                }
                if (renderedCount == 0)
                    return;
                UIKitBindScanResult scan = mOwnerKind.HasValue
                    ? UIKitBindScanner.ScanOwner(owner.gameObject, mOwnerKind.Value)
                    : UIKitBindScanner.Scan(owner.gameObject);
                if (scan.Diagnostics.Count == 0)
                {
                    body.Add(InspectorKitUi.CreateInfoBox(
                        "当前绑定定义全部有效。",
                        InspectorInfoBoxType.Success));
                    return;
                }
                AddDiagnostics(body, scan);
            });
        }

        /// <summary>把公共面板校验结果转换为 InspectorKit 信息框。</summary>
        private static void AddPanelValidation(
            VisualElement body,
            UIPanelValidationResult validation)
        {
            if (validation.Issues.Count == 0)
            {
                body.Add(InspectorKitUi.CreateInfoBox(
                    "当前面板配置全部有效。",
                    InspectorInfoBoxType.Success));
                return;
            }

            for (var index = 0; index < validation.Issues.Count; index++)
            {
                UIPanelValidationIssue issue = validation.Issues[index];
                InspectorInfoBoxType type = GetValidationInfoBoxType(issue.Severity);
                string prefix = string.IsNullOrEmpty(issue.Path) ? string.Empty : issue.Path + ": ";
                string suffix = string.IsNullOrEmpty(issue.FixSuggestion)
                    ? string.Empty
                    : "\n" + issue.FixSuggestion;
                body.Add(InspectorKitUi.CreateInfoBox(
                    prefix + issue.Message + suffix,
                    type));
            }
        }

        /// <summary>把公共校验严重度映射为 InspectorKit 信息框样式。</summary>
        private static InspectorInfoBoxType GetValidationInfoBoxType(
            UIPanelValidationSeverity severity)
        {
            if (severity == UIPanelValidationSeverity.Error) return InspectorInfoBoxType.Error;
            if (severity == UIPanelValidationSeverity.Warning) return InspectorInfoBoxType.Warning;
            return InspectorInfoBoxType.Info;
        }

        /// <summary>把扫描诊断逐项追加到 InspectorKit 容器。</summary>
        private static void AddDiagnostics(VisualElement body, UIKitBindScanResult scan)
        {
            for (var index = 0; index < scan.Diagnostics.Count; index++)
            {
                UIKitBindDiagnostic diagnostic = scan.Diagnostics[index];
                InspectorInfoBoxType type = diagnostic.Severity == UIKitBindDiagnosticSeverity.Error
                    ? InspectorInfoBoxType.Error
                    : InspectorInfoBoxType.Warning;
                body.Add(InspectorKitUi.CreateInfoBox(
                    diagnostic.Path + ": " + diagnostic.Message,
                    type));
            }
        }

        /// <summary>从 EditorPrefs 恢复当前 owner 的折叠路径。</summary>
        private void LoadCollapsedPaths()
        {
            mCollapsedPaths.Clear();
            string raw = EditorPrefs.GetString(GetCollapsedPathsKey(), string.Empty);
            string[] paths = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < paths.Length; index++)
                mCollapsedPaths.Add(paths[index]);
        }

        /// <summary>按稳定顺序保存当前 owner 的折叠路径。</summary>
        private void SaveCollapsedPaths()
        {
            List<string> paths = new(mCollapsedPaths);
            paths.Sort(StringComparer.Ordinal);
            EditorPrefs.SetString(GetCollapsedPathsKey(), string.Join("\n", paths));
        }

        /// <summary>构造绑定树折叠状态的项目内稳定键。</summary>
        private string GetCollapsedPathsKey()
        {
            Component owner = mOwnerProvider();
            return COLLAPSED_PATHS_PREFIX + GetOwnerIdentity(owner);
        }

        /// <summary>优先使用 Prefab 路径，否则使用场景和层级路径标识 owner。</summary>
        private static string GetOwnerIdentity(Component owner)
        {
            if (owner == default)
                return "Unknown";
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(owner.gameObject);
            if (!string.IsNullOrEmpty(assetPath))
                return owner.GetType().FullName + "|" + assetPath;
            string scene = owner.gameObject.scene.path;
            string sceneId = string.IsNullOrEmpty(scene) ? "UnsavedScene" : scene;
            return owner.GetType().FullName + "|" + sceneId + "|" + BuildTransformPath(owner.transform);
        }

        /// <summary>构造 Transform 从层级根到自身的稳定路径。</summary>
        private static string BuildTransformPath(Transform transform)
        {
            List<string> names = new();
            Transform current = transform;
            while (current != default)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        /// <summary>返回 BindType 的稳定 Inspector 强调色。</summary>
        private static Color GetColor(BindType bindType)
        {
            switch (bindType)
            {
                case BindType.Member:
                    return new Color(0.4f, 0.6f, 0.9f);
                case BindType.Element:
                    return new Color(0.4f, 0.8f, 0.4f);
                case BindType.Component:
                    return new Color(0.9f, 0.6f, 0.3f);
                default:
                    return new Color(0.6f, 0.6f, 0.6f);
            }
        }

        /// <summary>保存一个只读 Inspector 绑定节点。</summary>
        private sealed class InspectorBindingNode
        {
            /// <summary>创建稳定层级节点。</summary>
            internal InspectorBindingNode(
                string name,
                string typeName,
                string path,
                BindType bindType,
                GameObject gameObject)
            {
                Name = name;
                TypeName = typeName;
                Path = path;
                BindType = bindType;
                GameObject = gameObject;
            }

            internal string Name { get; }
            internal string TypeName { get; }
            internal string Path { get; }
            internal BindType BindType { get; }
            internal GameObject GameObject { get; }
            internal List<InspectorBindingNode> Children { get; } = new();
        }

        /// <summary>累计绑定树各类型节点数量。</summary>
        private sealed class BindingTreeStats
        {
            internal int Total { get; private set; }
            internal int Member { get; private set; }
            internal int Element { get; private set; }
            internal int Component { get; private set; }
            internal int Leaf { get; private set; }

            /// <summary>把一个 BindType 计入总量和对应分类。</summary>
            internal void Add(BindType bindType)
            {
                Total++;
                switch (bindType)
                {
                    case BindType.Member:
                        Member++;
                        break;
                    case BindType.Element:
                        Element++;
                        break;
                    case BindType.Component:
                        Component++;
                        break;
                    default:
                        Leaf++;
                        break;
                }
            }
        }
    }
}
#endif
