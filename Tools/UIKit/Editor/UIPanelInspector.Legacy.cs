#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// UIPanelInspector - 旧版兼容方法（保留以支持可能的外部调用）
    /// </summary>
    public partial class UIPanelInspector
    {
        /// <summary>
        /// 创建绑定树节点卡片（嵌套样式 - 已弃用，保留兼容）
        /// </summary>
        /// <param name="node">绑定树节点</param>
        /// <param name="errorPaths">错误节点路径集合</param>
        /// <param name="nestLevel">嵌套层级（用于计算背景色深度）</param>
        private VisualElement CreateBindTreeNodeCard(BindTreeNode node, HashSet<string> errorPaths, int nestLevel)
        {
            bool hasError = node.HasErrors || (node.Path != null && errorPaths.Contains(node.Path));
            bool hasWarning = node.HasWarnings;
            bool hasSubtreeError = node.HasErrorsInSubtree;
            
            // 收集有 Bind 组件的子节点
            var bindChildren = new List<BindTreeNode>(4);
            CollectBindChildren(node, bindChildren);
            bool hasChildren = bindChildren.Count > 0;
            
            // 获取节点颜色
            Color nodeColor = GetBindTypeColor(node.Type);
            
            // 根据嵌套层级计算背景色（越深越暗）
            float baseBrightness = 0.22f - nestLevel * 0.03f;
            baseBrightness = Mathf.Max(baseBrightness, 0.12f);
            Color bgColor = hasError 
                ? new Color(0.45f, 0.15f, 0.15f) 
                : new Color(baseBrightness, baseBrightness, baseBrightness);
            Color borderColor = hasError ? COLOR_ERROR : nodeColor;

            // 外层卡片容器
            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(bgColor);
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = new StyleColor(borderColor);
            card.style.marginBottom = 4;
            card.style.paddingTop = 6;
            card.style.paddingBottom = 6;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 6;
            card.style.minHeight = 28;
            card.style.flexShrink = 0;
            
            // 节点头部行
            var headerRow = CreateLegacyHeaderRow(node, hasError, hasWarning, hasSubtreeError, nodeColor);
            card.Add(headerRow);
            
            // 子节点容器（真正的嵌套）
            if (hasChildren)
            {
                var childrenContainer = new VisualElement();
                childrenContainer.style.marginTop = 6;
                childrenContainer.style.marginLeft = 8;
                childrenContainer.style.paddingLeft = 8;
                childrenContainer.style.borderLeftWidth = 1;
                childrenContainer.style.borderLeftColor = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
                
                foreach (var child in bindChildren)
                {
                    var childCard = CreateBindTreeNodeCard(child, errorPaths, nestLevel + 1);
                    childrenContainer.Add(childCard);
                }
                
                card.Add(childrenContainer);
            }
            
            // 点击定位功能
            RegisterCardClickHandler(card, node, hasError, bgColor);
            
            return card;
        }

        /// <summary>
        /// 创建旧版头部行
        /// </summary>
        private VisualElement CreateLegacyHeaderRow(BindTreeNode node, bool hasError, bool hasWarning, 
            bool hasSubtreeError, Color nodeColor)
        {
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.minHeight = 20;
            
            // 错误/警告标记
            if (hasError)
            {
                var errorBadge = CreateStatusBadge(COLOR_ERROR, KitIcons.ERROR, Color.white, GetValidationTooltip(node));
                headerRow.Add(errorBadge);
            }
            else if (hasWarning)
            {
                var warnBadge = CreateStatusBadge(COLOR_WARNING, KitIcons.WARNING, new Color(0.2f, 0.2f, 0.2f), GetValidationTooltip(node));
                headerRow.Add(warnBadge);
            }
            
            // 类型图标
            string icon = GetBindTypeIcon(node.Type);
            var iconLabel = new Label(icon);
            iconLabel.style.color = new StyleColor(nodeColor);
            iconLabel.style.marginRight = 6;
            iconLabel.style.fontSize = 12;
            headerRow.Add(iconLabel);
            
            // 名称
            var nameLabel = new Label(node.Name);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new StyleColor(new Color(0.95f, 0.95f, 0.95f));
            nameLabel.style.marginRight = 8;
            headerRow.Add(nameLabel);
            
            // 类型信息
            string typeInfo = GetShortTypeName(node.ComponentTypeName);
            if (!string.IsNullOrEmpty(typeInfo))
            {
                var typeLabel = new Label($"({typeInfo})");
                typeLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                typeLabel.style.fontSize = 11;
                typeLabel.style.marginRight = 8;
                headerRow.Add(typeLabel);
            }

            // 绑定类型标签
            var bindTypeLabel = new Label($"- {node.Type}");
            bindTypeLabel.style.color = new StyleColor(nodeColor);
            bindTypeLabel.style.fontSize = 11;
            headerRow.Add(bindTypeLabel);
            
            // 子树错误指示器
            if (!hasError && hasSubtreeError)
            {
                var subtreeErrorIcon = new Image { image = KitIcons.GetTexture(KitIcons.ERROR) };
                subtreeErrorIcon.style.width = 12;
                subtreeErrorIcon.style.height = 12;
                subtreeErrorIcon.style.marginLeft = 8;
                subtreeErrorIcon.tintColor = COLOR_ERROR;
                subtreeErrorIcon.tooltip = "子节点存在错误";
                headerRow.Add(subtreeErrorIcon);
            }
            
            return headerRow;
        }
        
        /// <summary>
        /// 创建状态徽章
        /// </summary>
        private VisualElement CreateStatusBadge(Color bgColor, string iconName, Color iconTint, string tooltip)
        {
            var badge = new VisualElement();
            badge.style.width = 18;
            badge.style.height = 18;
            badge.style.backgroundColor = new StyleColor(bgColor);
            badge.style.borderTopLeftRadius = 9;
            badge.style.borderTopRightRadius = 9;
            badge.style.borderBottomLeftRadius = 9;
            badge.style.borderBottomRightRadius = 9;
            badge.style.marginRight = 6;
            badge.style.alignItems = Align.Center;
            badge.style.justifyContent = Justify.Center;
            badge.tooltip = tooltip;
            
            var icon = new Image { image = KitIcons.GetTexture(iconName) };
            icon.style.width = 14;
            icon.style.height = 14;
            icon.tintColor = iconTint;
            badge.Add(icon);
            
            return badge;
        }
        
        /// <summary>
        /// 递归收集所有有 Bind 组件的子节点
        /// </summary>
        private void CollectBindChildren(BindTreeNode node, List<BindTreeNode> result)
        {
            if (node.Children == null) return;
            
            foreach (var child in node.Children)
            {
                if (child.Bind != null)
                {
                    result.Add(child);
                }
                else
                {
                    // 没有 Bind 的中间节点，继续向下搜索
                    CollectBindChildren(child, result);
                }
            }
        }
    }
}
#endif
