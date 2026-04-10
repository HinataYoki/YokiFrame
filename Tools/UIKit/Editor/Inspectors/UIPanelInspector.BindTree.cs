#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// <see cref="UIPanelInspector"/> 的绑定树区块。
    /// 用于展示当前面板的绑定层级以及校验汇总结果。
    /// </summary>
    public partial class UIPanelInspector
    {
        /// <summary>
        /// 创建绑定树区块。
        /// </summary>
        private void CreateBindTreeSection()
        {
            var panel = target as UIPanel;
            if (panel == null)
                return;

            var section = new VisualElement();
            section.AddToClassList("uipanel-section");
            section.AddToClassList("uipanel-section-bindtree");

            bool savedFoldoutState = SessionState.GetBool(KEY_BIND_TREE_FOLDOUT, true);
            mBindTreeFoldout = new Foldout { text = "绑定树", value = savedFoldoutState };
            mBindTreeFoldout.AddToClassList("uipanel-bindtree-foldout");

            mBindTreeFoldout.RegisterValueChangedCallback(evt =>
            {
                SessionState.SetBool(KEY_BIND_TREE_FOLDOUT, evt.newValue);
            });

            section.Add(mBindTreeFoldout);

            var content = new VisualElement();
            content.AddToClassList("uipanel-section-content");
            mBindTreeFoldout.Add(content);

            var openCodeBtn = CreateOpenCodeButton(panel);
            if (openCodeBtn != null)
            {
                var btnRow = new VisualElement();
                btnRow.style.flexDirection = FlexDirection.Row;
                btnRow.style.justifyContent = Justify.FlexEnd;
                btnRow.style.marginBottom = 8;
                btnRow.Add(openCodeBtn);
                content.Add(btnRow);
            }

            mBindTreeContainer = new VisualElement();
            mBindTreeContainer.AddToClassList("uipanel-bindtree-container");
            mBindTreeContainer.style.marginBottom = 8;
            mBindTreeContainer.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            mBindTreeContainer.style.borderTopLeftRadius = 6;
            mBindTreeContainer.style.borderTopRightRadius = 6;
            mBindTreeContainer.style.borderBottomLeftRadius = 6;
            mBindTreeContainer.style.borderBottomRightRadius = 6;
            mBindTreeContainer.style.paddingTop = 8;
            mBindTreeContainer.style.paddingBottom = 8;
            mBindTreeContainer.style.paddingLeft = 8;
            mBindTreeContainer.style.paddingRight = 8;
            content.Add(mBindTreeContainer);

            var legend = CreateBindTreeLegend();
            content.Add(legend);

            mBindStatsLabel = new Label();
            mBindStatsLabel.AddToClassList("uipanel-bindtree-stats");
            content.Add(mBindStatsLabel);

            mValidationSummaryLabel = new Label();
            mValidationSummaryLabel.AddToClassList("uipanel-validation-summary");
            content.Add(mValidationSummaryLabel);

            var refreshBtn = new Button(RefreshBindTree);
            refreshBtn.AddToClassList("uipanel-refresh-btn");
            ApplyRefreshButtonStyle(refreshBtn);
            content.Add(refreshBtn);

            mRoot.Add(section);
            mLastSection = section;

            RefreshBindTree();
        }

        /// <summary>
        /// 应用刷新按钮的统一样式。
        /// </summary>
        private void ApplyRefreshButtonStyle(Button btn)
        {
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems = Align.Center;
            btn.style.justifyContent = Justify.Center;
            btn.style.height = 28;
            btn.style.marginTop = 4;

            var icon = new Image { image = KitIcons.GetTexture(KitIcons.REFRESH) };
            icon.style.width = 14;
            icon.style.height = 14;
            icon.style.marginRight = 6;
            btn.Add(icon);

            var label = new Label("刷新绑定树");
            label.style.fontSize = 12;
            btn.Add(label);
        }

        /// <summary>
        /// 创建绑定树图例。
        /// </summary>
        private VisualElement CreateBindTreeLegend()
        {
            var legend = new VisualElement();
            legend.AddToClassList("uipanel-bindtree-legend");
            legend.style.flexDirection = FlexDirection.Row;
            legend.style.flexWrap = Wrap.Wrap;
            legend.style.marginTop = 8;
            legend.style.marginBottom = 4;

            var items = new[]
            {
                (KitIcons.DIAMOND, "成员", COLOR_MEMBER),
                (KitIcons.DOT_FILLED, "元素", COLOR_ELEMENT),
                (KitIcons.DOT_FILLED, "组件", COLOR_COMPONENT),
                (KitIcons.DOT_EMPTY, "叶子节点", COLOR_LEAF)
            };

            foreach (var (iconId, label, color) in items)
            {
                var item = new VisualElement();
                item.AddToClassList("uipanel-legend-item");
                item.style.flexDirection = FlexDirection.Row;
                item.style.alignItems = Align.Center;
                item.style.marginRight = 12;

                var iconImg = new Image { image = KitIcons.GetTexture(iconId) };
                iconImg.style.width = 12;
                iconImg.style.height = 12;
                iconImg.tintColor = color;
                iconImg.style.marginRight = 4;
                item.Add(iconImg);

                var textLabel = new Label(label);
                textLabel.AddToClassList("uipanel-legend-text");
                textLabel.style.fontSize = 11;
                textLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                item.Add(textLabel);

                legend.Add(item);
            }

            return legend;
        }

        /// <summary>
        /// 刷新绑定树显示内容。
        /// </summary>
        private void RefreshBindTree()
        {
            var panel = target as UIPanel;
            if (panel == null || mBindTreeContainer == null)
                return;

            mBindTreeContainer.Clear();

            var tree = BindService.CollectBindTree(panel.gameObject);
            if (tree != null)
            {
                BindValidator.ValidateTree(tree);
            }

            if (tree == null || tree.Children == null || tree.Children.Count == 0)
            {
                var emptyLabel = new Label("未找到任何绑定信息");
                emptyLabel.AddToClassList("uipanel-bindtree-empty");
                emptyLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.paddingTop = 20;
                emptyLabel.style.paddingBottom = 20;
                mBindTreeContainer.Add(emptyLabel);

                mBindStatsLabel.text = string.Empty;
                mValidationSummaryLabel.text = string.Empty;
                return;
            }

            var errorNodePaths = new HashSet<string>();
            CollectErrorNodePaths(tree, errorNodePaths);

            BuildBindTreeViewNested(tree, mBindTreeContainer, errorNodePaths);

            var stats = BindService.GetBindStatistics(panel.gameObject);
            mBindStatsLabel.text = stats.ToString();

            UpdateValidationSummary(panel.gameObject);
        }

        /// <summary>
        /// 收集当前存在校验错误的节点路径。
        /// </summary>
        private void CollectErrorNodePaths(BindTreeNode node, HashSet<string> errorPaths)
        {
            if (node == null)
                return;

            if (node.HasErrors && !string.IsNullOrEmpty(node.Path))
            {
                errorPaths.Add(node.Path);
            }

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    CollectErrorNodePaths(child, errorPaths);
                }
            }
        }

        /// <summary>
        /// 递归构建绑定树的扁平显示结构。
        /// </summary>
        private void BuildBindTreeViewNested(BindTreeNode node, VisualElement parent, HashSet<string> errorPaths)
        {
            if (node == null)
                return;

            var flatNodes = new List<(BindTreeNode Node, int Level, bool HasChildren)>(16);
            CollectFlatNodes(node, flatNodes, 0, null);

            foreach (var (bindNode, level, hasChildren) in flatNodes)
            {
                var nodeRow = CreateBindTreeNodeRow(bindNode, level, errorPaths, hasChildren);
                parent.Add(nodeRow);
            }
        }

        /// <summary>
        /// 在考虑折叠状态的前提下，将绑定节点收集为扁平列表。
        /// </summary>
        private void CollectFlatNodes(BindTreeNode node, List<(BindTreeNode, int, bool)> result, int level, string parentPath)
        {
            if (node == null)
                return;

            string currentPath = node.Path;

            if (node.Bind != null)
            {
                bool hasBindChildren = HasBindChildren(node);
                result.Add((node, level, hasBindChildren));

                if (mCollapsedNodes.Contains(currentPath))
                {
                    return;
                }

                level++;
                parentPath = currentPath;
            }

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    CollectFlatNodes(child, result, level, parentPath);
                }
            }
        }

        /// <summary>
        /// 判断当前节点下是否仍存在绑定子节点。
        /// </summary>
        private bool HasBindChildren(BindTreeNode node)
        {
            if (node.Children == null)
                return false;

            foreach (var child in node.Children)
            {
                if (child.Bind != null)
                    return true;

                if (HasBindChildren(child))
                    return true;
            }

            return false;
        }
    }
}
#endif
