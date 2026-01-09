#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// UIPanelInspector - 绑定树区块
    /// </summary>
    public partial class UIPanelInspector
    {
        /// <summary>
        /// 创建绑定关系区块
        /// </summary>
        private void CreateBindTreeSection()
        {
            var panel = target as UIPanel;
            if (panel == null) return;
            
            var section = new VisualElement();
            section.AddToClassList("uipanel-section");
            section.AddToClassList("uipanel-section-bindtree");
            
            // 可折叠标题
            mBindTreeFoldout = new Foldout { text = "绑定关系", value = true };
            mBindTreeFoldout.AddToClassList("uipanel-bindtree-foldout");
            section.Add(mBindTreeFoldout);
            
            // 内容容器
            var content = new VisualElement();
            content.AddToClassList("uipanel-section-content");
            mBindTreeFoldout.Add(content);
            
            // 绑定树容器（自适应高度，不使用滚动条）
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
            
            // 图例
            var legend = CreateBindTreeLegend();
            content.Add(legend);
            
            // 统计信息
            mBindStatsLabel = new Label();
            mBindStatsLabel.AddToClassList("uipanel-bindtree-stats");
            content.Add(mBindStatsLabel);
            
            // 验证结果摘要
            mValidationSummaryLabel = new Label();
            mValidationSummaryLabel.AddToClassList("uipanel-validation-summary");
            content.Add(mValidationSummaryLabel);
            
            // 刷新按钮
            var refreshBtn = new Button(RefreshBindTree);
            refreshBtn.AddToClassList("uipanel-refresh-btn");
            ApplyRefreshButtonStyle(refreshBtn);
            content.Add(refreshBtn);
            
            mRoot.Add(section);
            mLastSection = section;
            
            // 初始化绑定树
            RefreshBindTree();
        }
        
        /// <summary>
        /// 应用刷新按钮样式
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
        /// 创建绑定树图例
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
                (KitIcons.DIAMOND, "Member", COLOR_MEMBER),
                (KitIcons.DOT_FILLED, "Element", COLOR_ELEMENT),
                (KitIcons.DOT_FILLED, "Component", COLOR_COMPONENT),
                (KitIcons.DOT_EMPTY, "Leaf", COLOR_LEAF)
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
        /// 刷新绑定树显示
        /// </summary>
        private void RefreshBindTree()
        {
            var panel = target as UIPanel;
            if (panel == null || mBindTreeContainer == null) return;
            
            mBindTreeContainer.Clear();
            
            // 收集绑定树
            var tree = BindService.CollectBindTree(panel.gameObject);
            
            // 立即执行验证，将结果关联到节点
            if (tree != null)
            {
                BindValidator.ValidateTree(tree);
            }
            if (tree == null || tree.Children == null || tree.Children.Count == 0)
            {
                var emptyLabel = new Label("暂无绑定");
                emptyLabel.AddToClassList("uipanel-bindtree-empty");
                emptyLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.paddingTop = 20;
                emptyLabel.style.paddingBottom = 20;
                mBindTreeContainer.Add(emptyLabel);
                
                mBindStatsLabel.text = "";
                mValidationSummaryLabel.text = "";
                return;
            }
            
            // 收集所有有错误的节点路径，用于高亮显示
            var errorNodePaths = new HashSet<string>();
            CollectErrorNodePaths(tree, errorNodePaths);
            
            // 构建树视图（使用嵌套卡片样式）
            BuildBindTreeViewNested(tree, mBindTreeContainer, errorNodePaths);
            
            // 更新统计信息
            var stats = BindService.GetBindStatistics(panel.gameObject);
            mBindStatsLabel.text = stats.ToString();
            
            // 更新验证结果
            UpdateValidationSummary(panel.gameObject);
        }
        
        /// <summary>
        /// 收集所有有错误的节点路径
        /// </summary>
        private void CollectErrorNodePaths(BindTreeNode node, HashSet<string> errorPaths)
        {
            if (node == null) return;
            
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
        /// 构建扁平化的绑定树视图（使用缩进线表示层级，支持折叠）
        /// </summary>
        private void BuildBindTreeViewNested(BindTreeNode node, VisualElement parent, HashSet<string> errorPaths)
        {
            if (node == null) return;
            
            // 收集所有有 Bind 的节点到扁平列表（考虑折叠状态）
            var flatNodes = new List<(BindTreeNode Node, int Level, bool HasChildren)>(16);
            CollectFlatNodes(node, flatNodes, 0, null);
            
            // 逐个创建节点行
            foreach (var (bindNode, level, hasChildren) in flatNodes)
            {
                var nodeRow = CreateBindTreeNodeRow(bindNode, level, errorPaths, hasChildren);
                parent.Add(nodeRow);
            }
        }
        
        /// <summary>
        /// 递归收集所有有 Bind 组件的节点到扁平列表（考虑折叠状态）
        /// </summary>
        /// <param name="node">当前节点</param>
        /// <param name="result">结果列表</param>
        /// <param name="level">当前层级</param>
        /// <param name="parentPath">父节点路径（用于判断是否被折叠）</param>
        private void CollectFlatNodes(BindTreeNode node, List<(BindTreeNode, int, bool)> result, int level, string parentPath)
        {
            if (node == null) return;
            
            string currentPath = node.Path;
            
            // 如果有 Bind 组件，添加到列表
            if (node.Bind != null)
            {
                // 检查是否有子节点（有 Bind 的子节点）
                bool hasBindChildren = HasBindChildren(node);
                result.Add((node, level, hasBindChildren));
                
                // 如果当前节点被折叠，不处理子节点
                if (mCollapsedNodes.Contains(currentPath))
                {
                    return;
                }
                
                level++; // 子节点层级 +1
                parentPath = currentPath;
            }
            
            // 递归处理子节点
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    CollectFlatNodes(child, result, level, parentPath);
                }
            }
        }
        
        /// <summary>
        /// 检查节点是否有带 Bind 组件的子节点
        /// </summary>
        private bool HasBindChildren(BindTreeNode node)
        {
            if (node.Children == null) return false;
            
            foreach (var child in node.Children)
            {
                if (child.Bind != null) return true;
                if (HasBindChildren(child)) return true;
            }
            return false;
        }
        
        /// <summary>
        /// 递归构建绑定树视图（旧方法，保留兼容）
        /// </summary>
        private void BuildBindTreeView(BindTreeNode node, VisualElement parent, int depth)
        {
            if (node == null) return;
            
            // 只显示有 Bind 组件的节点
            if (node.Bind != null)
            {
                var nodeElement = CreateBindTreeNodeElement(node, depth);
                parent.Add(nodeElement);
            }
            
            // 递归处理子节点
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    BuildBindTreeView(child, parent, depth + 1);
                }
            }
        }
        
        /// <summary>
        /// 创建绑定树节点元素（旧方法，保留兼容）
        /// </summary>
        private VisualElement CreateBindTreeNodeElement(BindTreeNode node, int depth)
        {
            // 使用新的卡片样式
            return CreateBindTreeNodeCard(node, new HashSet<string>(), depth);
        }
    }
}
#endif
