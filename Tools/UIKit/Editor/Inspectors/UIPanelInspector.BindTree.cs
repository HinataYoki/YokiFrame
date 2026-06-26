#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    public partial class UIPanelInspector
    {
        private const string BIND_TREE_FOLDOUT_KEY = "YokiFrame.UIKit.UIPanelInspector.BindTree";
        private const string BIND_TREE_COLLAPSED_PATHS_KEY = "YokiFrame.UIKit.UIPanelInspector.CollapsedBindPaths";

        private readonly HashSet<string> mCollapsedBindPaths = new();

        private VisualElement mBindTreeContainer;
        private Label mBindStatsLabel;
        private Label mBindValidationLabel;

        private void CreateBindTree(VisualElement root)
        {
            var section = CreateSectionContainer("uipanel-section-bindtree");

            var foldout = new Foldout
            {
                text = "绑定树",
                value = SessionState.GetBool(BIND_TREE_FOLDOUT_KEY, true)
            };
            foldout.AddToClassList("uipanel-bindtree-foldout");
            foldout.RegisterValueChangedCallback(evt => SessionState.SetBool(BIND_TREE_FOLDOUT_KEY, evt.newValue));
            section.Add(foldout);

            var content = new VisualElement();
            content.AddToClassList("uipanel-section-content");
            foldout.Add(content);

            var toolbar = new VisualElement();
            toolbar.AddToClassList("uipanel-bindtree-toolbar");
            var openButton = new Button(OpenPanelScript) { text = "打开脚本" };
            openButton.AddToClassList("uipanel-open-code-btn");
            toolbar.Add(openButton);
            content.Add(toolbar);

            mBindTreeContainer = new VisualElement();
            mBindTreeContainer.AddToClassList("uipanel-bindtree-container");
            content.Add(mBindTreeContainer);

            content.Add(CreateBindTreeLegend());

            mBindStatsLabel = new Label();
            mBindStatsLabel.AddToClassList("uipanel-bindtree-stats");
            content.Add(mBindStatsLabel);

            mBindValidationLabel = new Label();
            mBindValidationLabel.AddToClassList("uipanel-validation-summary");
            content.Add(mBindValidationLabel);

            var refreshButton = new Button(RefreshBindTree) { text = "刷新绑定树" };
            refreshButton.AddToClassList("uipanel-refresh-btn");
            content.Add(refreshButton);

            var generateButton = new Button(GenerateUICode) { text = "生成 UI 代码" };
            generateButton.AddToClassList("uipanel-gencode-btn");
            content.Add(generateButton);

            root.Add(section);
            RefreshBindTree();
        }

        private VisualElement CreateBindTreeLegend()
        {
            var legend = new VisualElement();
            legend.AddToClassList("uipanel-bindtree-legend");
            legend.Add(CreateLegendItem(GetBindMarker(BindType.Member), "Member", GetBindColor(BindType.Member)));
            legend.Add(CreateLegendItem(GetBindMarker(BindType.Element), "Element", GetBindColor(BindType.Element)));
            legend.Add(CreateLegendItem(GetBindMarker(BindType.Component), "Component", GetBindColor(BindType.Component)));
            legend.Add(CreateLegendItem(GetBindMarker(BindType.Leaf), "Leaf", GetBindColor(BindType.Leaf)));
            return legend;
        }

        private static VisualElement CreateLegendItem(string markerText, string labelText, Color color)
        {
            var item = new VisualElement();
            item.AddToClassList("uipanel-legend-item");

            var marker = new Label(markerText);
            marker.AddToClassList("uipanel-legend-icon");
            marker.style.color = new StyleColor(color);
            item.Add(marker);

            var label = new Label(labelText);
            label.AddToClassList("uipanel-legend-text");
            item.Add(label);
            return item;
        }

        private void RefreshBindTree()
        {
            if (mBindTreeContainer == null)
                return;

            mBindTreeContainer.Clear();
            var panel = target as UIPanel;
            if (panel == null)
                return;

            var rootInfo = CollectBindInfo(panel.gameObject);
            var stats = new BindTreeStats();
            var errors = new List<string>(4);
            var renderedCount = RenderBindChildren(rootInfo, 0, stats, errors);

            if (renderedCount == 0)
            {
                var empty = new Label("未找到任何绑定信息");
                empty.AddToClassList("uipanel-bindtree-empty");
                mBindTreeContainer.Add(empty);
                mBindStatsLabel.text = string.Empty;
                mBindValidationLabel.text = string.Empty;
                mBindValidationLabel.RemoveFromClassList("validation-success");
                mBindValidationLabel.RemoveFromClassList("validation-has-errors");
                return;
            }

            mBindStatsLabel.text = "共 " + stats.Total + " 个绑定（" +
                                   stats.Member + " Member, " +
                                   stats.Element + " Element, " +
                                   stats.Component + " Component, " +
                                   stats.Leaf + " Leaf）";

            mBindValidationLabel.RemoveFromClassList("validation-success");
            mBindValidationLabel.RemoveFromClassList("validation-has-errors");
            if (errors.Count == 0)
            {
                mBindValidationLabel.text = "当前绑定定义全部有效。";
                mBindValidationLabel.AddToClassList("validation-success");
            }
            else
            {
                mBindValidationLabel.text = string.Join("\n", errors.ToArray());
                mBindValidationLabel.AddToClassList("validation-has-errors");
            }
        }

        private static BindCodeInfo CollectBindInfo(GameObject root)
        {
            var info = new BindCodeInfo
            {
                Name = root.name,
                Type = root.name,
                Bind = BindType.Member,
                Self = root
            };
            CollectInspectorBinds(root.transform, root.name, info);
            return info;
        }

        private static void CollectInspectorBinds(Transform current, string fullName, BindCodeInfo parentInfo)
        {
            foreach (Transform child in current)
            {
                var nextFullName = fullName + "/" + child.name;

                if (child.TryGetComponent<AbstractBind>(out var bind))
                {
                    ProcessInspectorBind(bind, child, nextFullName, parentInfo);
                }
                else
                {
                    CollectInspectorBinds(child, nextFullName, parentInfo);
                }
            }
        }

        private static void ProcessInspectorBind(AbstractBind bind, Transform child, string nextFullName, BindCodeInfo parentInfo)
        {
            var strategy = BindStrategyRegistry.Get(bind.Bind);
            if (strategy == null || strategy.ShouldSkipCodeGen)
                return;

            var bindType = !string.IsNullOrEmpty(bind.Type) ? bind.Type : strategy.InferTypeName(bind);
            var bindName = string.IsNullOrEmpty(bind.Name) ? child.name : bind.Name;
            var repeat = parentInfo.MemberDic.ContainsKey(bindName);

            var order = parentInfo.MemberDic.Count + 1;
            var bindInfo = new BindCodeInfo
            {
                Type = bindType,
                Name = bindName,
                Comment = bind.Comment,
                PathToRoot = nextFullName,
                Bind = bind.Bind,
                Self = child.gameObject,
                BindScript = bind,
                RepeatElement = repeat,
                Order = order,
            };

            var key = repeat ? bindName + order : bindName;
            parentInfo.MemberDic.Add(key, bindInfo);

            var nextParent = strategy.CanContainChildren ? bindInfo : parentInfo;
            CollectInspectorBinds(child, nextFullName, nextParent);
        }

        private int RenderBindChildren(BindCodeInfo parent, int level, BindTreeStats stats, List<string> errors)
        {
            var renderedCount = 0;
            var children = GetSortedChildren(parent);
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                stats.Add(child.Bind);
                ValidateBindInfo(child, errors);

                var childHasChildren = HasBindChildren(child);
                mBindTreeContainer.Add(CreateBindRow(child, level, childHasChildren));
                renderedCount++;

                if (childHasChildren && !mCollapsedBindPaths.Contains(GetBindPathKey(child)))
                    renderedCount += RenderBindChildren(child, level + 1, stats, errors);
            }

            return renderedCount;
        }

        private VisualElement CreateBindRow(BindCodeInfo info, int level, bool hasChildren)
        {
            var row = new VisualElement();
            row.AddToClassList("uipanel-bindtree-node");
            row.AddToClassList("uipanel-bindtree-clickable");

            if (level > 0)
            {
                var indent = new VisualElement();
                indent.AddToClassList("uipanel-bindtree-indent");
                indent.style.width = level * 16;
                indent.style.minWidth = level * 16;
                row.Add(indent);
            }

            var card = new VisualElement();
            card.AddToClassList("uipanel-bindtree-card");
            card.style.borderLeftColor = new StyleColor(GetBindColor(info.Bind));
            row.Add(card);

            if (hasChildren)
            {
                var key = GetBindPathKey(info);
                var foldButton = new Button(() =>
                {
                    if (mCollapsedBindPaths.Contains(key))
                        mCollapsedBindPaths.Remove(key);
                    else
                        mCollapsedBindPaths.Add(key);
                    SaveCollapsedBindPaths();
                    RefreshBindTree();
                })
                {
                    text = mCollapsedBindPaths.Contains(key) ? ">" : "v"
                };
                foldButton.AddToClassList("uipanel-bindtree-fold-btn");
                card.Add(foldButton);
            }
            else
            {
                var spacer = new VisualElement();
                spacer.AddToClassList("uipanel-bindtree-fold-spacer");
                card.Add(spacer);
            }

            var marker = new Label(GetBindMarker(info.Bind));
            marker.AddToClassList("uipanel-bindtree-icon");
            marker.style.color = new StyleColor(GetBindColor(info.Bind));
            card.Add(marker);

            var name = new Label(info.Name);
            name.AddToClassList("uipanel-bindtree-name");
            card.Add(name);

            var typeName = new Label("(" + ShortTypeName(info.Type) + ")");
            typeName.AddToClassList("uipanel-bindtree-type");
            card.Add(typeName);

            var bindType = new Label("- " + info.Bind);
            bindType.AddToClassList("uipanel-bindtree-bindtype");
            bindType.style.color = new StyleColor(GetBindColor(info.Bind));
            card.Add(bindType);

            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (info.Self != null)
                {
                    Selection.activeGameObject = info.Self;
                    EditorGUIUtility.PingObject(info.Self);
                }
                evt.StopPropagation();
            });

            return row;
        }

        private static List<BindCodeInfo> GetSortedChildren(BindCodeInfo parent)
        {
            var result = new List<BindCodeInfo>();
            if (parent == null || parent.MemberDic == null)
                return result;

            foreach (var pair in parent.MemberDic)
            {
                if (pair.Value != null)
                    result.Add(pair.Value);
            }
            result.Sort(CompareBindOrder);
            return result;
        }

        private static int CompareBindOrder(BindCodeInfo left, BindCodeInfo right)
        {
            return left.Order.CompareTo(right.Order);
        }

        private static bool HasBindChildren(BindCodeInfo info)
        {
            return info != null && info.MemberDic != null && info.MemberDic.Count > 0;
        }

        private static string GetBindPathKey(BindCodeInfo info)
        {
            if (info == null)
                return string.Empty;
            return string.IsNullOrEmpty(info.PathToRoot) ? info.Name : info.PathToRoot;
        }

        private static void ValidateBindInfo(BindCodeInfo info, List<string> errors)
        {
            if (info.Bind == BindType.Leaf)
                return;
            if (info.RepeatElement && info.Bind == BindType.Member)
                errors.Add(info.PathToRoot + ": 重复的 Member 名称 " + info.Name + "。");
            if (string.IsNullOrEmpty(info.Name))
                errors.Add(info.PathToRoot + ": 字段名称为空。");
            if (string.IsNullOrEmpty(info.Type))
                errors.Add(info.PathToRoot + ": 类型为空。");
        }

        private void LoadCollapsedBindPaths()
        {
            mCollapsedBindPaths.Clear();

            var raw = SessionState.GetString(GetCollapsedBindPathsSessionKey(), string.Empty);
            if (string.IsNullOrEmpty(raw))
                return;

            var paths = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < paths.Length; i++)
            {
                if (!string.IsNullOrEmpty(paths[i]))
                    mCollapsedBindPaths.Add(paths[i]);
            }
        }

        private void SaveCollapsedBindPaths()
        {
            var paths = new List<string>(mCollapsedBindPaths);
            paths.Sort(StringComparer.Ordinal);
            SessionState.SetString(GetCollapsedBindPathsSessionKey(), string.Join("\n", paths.ToArray()));
        }

        private string GetCollapsedBindPathsSessionKey()
        {
            var panel = target as UIPanel;
            if (panel == null)
                return BIND_TREE_COLLAPSED_PATHS_KEY;

            return BIND_TREE_COLLAPSED_PATHS_KEY + "." + GetPanelSessionKey(panel);
        }

        private static string GetPanelSessionKey(UIPanel panel)
        {
            var assetPath = AssetDatabase.GetAssetPath(panel.gameObject);
            if (!string.IsNullOrEmpty(assetPath))
                return assetPath;

            var scene = panel.gameObject.scene;
            var sceneKey = scene.IsValid() && !string.IsNullOrEmpty(scene.path) ? scene.path : scene.name;
            if (string.IsNullOrEmpty(sceneKey))
                sceneKey = "UnsavedScene";

            return sceneKey + "|" + GetTransformPath(panel.transform);
        }

        private static string GetTransformPath(Transform transform)
        {
            var names = new List<string>(8);
            var current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static Color GetBindColor(BindType type)
        {
            switch (type)
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

        private static string GetBindMarker(BindType type)
        {
            switch (type)
            {
                case BindType.Member:
                    return "◇";
                case BindType.Element:
                    return "●";
                case BindType.Component:
                    return "◆";
                default:
                    return "○";
            }
        }

        private static string ShortTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return string.Empty;

            var index = fullName.LastIndexOf('.');
            return index >= 0 && index < fullName.Length - 1 ? fullName.Substring(index + 1) : fullName;
        }

        private sealed class BindTreeStats
        {
            public int Total;
            public int Member;
            public int Element;
            public int Component;
            public int Leaf;

            public void Add(BindType type)
            {
                Total++;
                switch (type)
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
                    case BindType.Leaf:
                        Leaf++;
                        break;
                }
            }
        }
    }
}
#endif
