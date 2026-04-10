#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// UIPanelInspector - 节点渲染
    /// </summary>
    public partial class UIPanelInspector
    {
        #region 常量 - 绑定树样式

        // 绑定类型对应的颜色
        private static readonly Color COLOR_MEMBER = new(0.4f, 0.6f, 0.9f);      // 蓝色
        private static readonly Color COLOR_ELEMENT = new(0.4f, 0.8f, 0.4f);     // 绿色
        private static readonly Color COLOR_COMPONENT = new(0.9f, 0.6f, 0.3f);   // 橙色
        private static readonly Color COLOR_LEAF = new(0.6f, 0.6f, 0.6f);        // 灰色
        private static readonly Color COLOR_ERROR = new(0.8f, 0.3f, 0.3f);       // 红色
        private static readonly Color COLOR_WARNING = new(0.9f, 0.7f, 0.2f);     // 黄色

        #endregion

        /// <summary>
        /// 创建绑定树节点行（扁平样式，用缩进表示层级）
        /// </summary>
        private VisualElement CreateBindTreeNodeRow(BindTreeNode node, int level, HashSet<string> errorPaths, bool hasChildren)
        {
            bool hasError = node.HasErrors || (node.Path != null && errorPaths.Contains(node.Path));
            bool hasWarning = node.HasWarnings;
            bool hasSubtreeError = node.HasErrorsInSubtree && !hasError;
            bool isCollapsed = mCollapsedNodes.Contains(node.Path);
            
            Color nodeColor = GetBindTypeColor(node.Type);
            Color bgColor = hasError 
                ? new Color(0.4f, 0.15f, 0.15f) 
                : new Color(0.22f, 0.22f, 0.22f);
            
            // 行容器
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Stretch;
            row.style.marginBottom = 2;
            row.style.minHeight = 26;

            // 缩进区域（包含层级线）
            if (level > 0)
            {
                var indentContainer = new VisualElement();
                indentContainer.style.flexDirection = FlexDirection.Row;
                indentContainer.style.width = level * 16; // 每层 16px 缩进
                indentContainer.style.minWidth = level * 16;
                
                // 绘制层级连接线
                for (int i = 0; i < level; i++)
                {
                    var lineContainer = new VisualElement();
                    lineContainer.style.width = 16;
                    lineContainer.style.alignItems = Align.Center;
                    
                    var line = new VisualElement();
                    line.style.width = 1;
                    line.style.flexGrow = 1;
                    line.style.backgroundColor = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
                    lineContainer.Add(line);
                    
                    indentContainer.Add(lineContainer);
                }
                
                row.Add(indentContainer);
            }
            
            // 内容卡片
            var card = CreateNodeCard(node, hasError, hasWarning, hasSubtreeError, hasChildren, isCollapsed, nodeColor, bgColor);
            row.Add(card);
            return row;
        }
        
        /// <summary>
        /// 创建节点卡片
        /// </summary>
        private VisualElement CreateNodeCard(BindTreeNode node, bool hasError, bool hasWarning, 
            bool hasSubtreeError, bool hasChildren, bool isCollapsed, Color nodeColor, Color bgColor)
        {
            var card = new VisualElement();
            card.style.flexGrow = 1;
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.Center;
            card.style.backgroundColor = new StyleColor(bgColor);
            card.style.borderTopLeftRadius = 4;
            card.style.borderTopRightRadius = 4;
            card.style.borderBottomLeftRadius = 4;
            card.style.borderBottomRightRadius = 4;
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = new StyleColor(hasError ? COLOR_ERROR : nodeColor);
            card.style.paddingTop = 4;
            card.style.paddingBottom = 4;
            card.style.paddingLeft = 4;
            card.style.paddingRight = 8;
            card.style.minHeight = 24;

            // 折叠按钮（如果有子节点）
            if (hasChildren)
            {
                string nodePath = node.Path;
                var foldBtn = new Button(() =>
                {
                    if (mCollapsedNodes.Contains(nodePath))
                        mCollapsedNodes.Remove(nodePath);
                    else
                        mCollapsedNodes.Add(nodePath);
                    RefreshBindTree();
                });
                ApplyFoldButtonStyle(foldBtn, isCollapsed);
                card.Add(foldBtn);
            }
            else
            {
                // 占位，保持对齐
                var spacer = new VisualElement();
                spacer.style.width = 22;
                spacer.style.minWidth = 22;
                card.Add(spacer);
            }
            
            // 错误/警告图标
            AddStatusIcon(card, hasError, hasWarning, node);
            
            // 类型图标
            string iconId = GetBindTypeIconId(node.Type);
            var iconImg = new Image { image = KitIcons.GetTexture(iconId) };
            iconImg.style.width = 12;
            iconImg.style.height = 12;
            iconImg.tintColor = nodeColor;
            iconImg.style.marginRight = 6;
            card.Add(iconImg);
            
            // 名称
            var nameLabel = new Label(node.Name);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new StyleColor(new Color(0.95f, 0.95f, 0.95f));
            nameLabel.style.marginRight = 8;
            card.Add(nameLabel);
            
            // 类型信息
            string typeInfo = GetShortTypeName(node.ComponentTypeName);
            if (!string.IsNullOrEmpty(typeInfo))
            {
                var typeLabel = new Label($"({typeInfo})");
                typeLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                typeLabel.style.fontSize = 11;
                typeLabel.style.marginRight = 8;
                card.Add(typeLabel);
            }

            // 绑定类型
            var bindTypeLabel = new Label($"- {node.Type}");
            bindTypeLabel.style.color = new StyleColor(nodeColor);
            bindTypeLabel.style.fontSize = 11;
            card.Add(bindTypeLabel);
            
            // 子树错误指示
            if (hasSubtreeError)
            {
                var subtreeErrorIcon = new Image { image = KitIcons.GetTexture(KitIcons.ERROR) };
                subtreeErrorIcon.style.width = 12;
                subtreeErrorIcon.style.height = 12;
                subtreeErrorIcon.style.marginLeft = 8;
                subtreeErrorIcon.tintColor = COLOR_ERROR;
                subtreeErrorIcon.tooltip = "子节点存在错误";
                card.Add(subtreeErrorIcon);
            }
            
            // 点击定位
            RegisterCardClickHandler(card, node, hasError, bgColor);
            
            return card;
        }
        
        /// <summary>
        /// 应用折叠按钮样式
        /// </summary>
        private void ApplyFoldButtonStyle(Button foldBtn, bool isCollapsed)
        {
            foldBtn.style.width = 18;
            foldBtn.style.height = 18;
            foldBtn.style.marginRight = 4;
            foldBtn.style.paddingLeft = 0;
            foldBtn.style.paddingRight = 0;
            foldBtn.style.paddingTop = 0;
            foldBtn.style.paddingBottom = 0;
            foldBtn.style.backgroundColor = new StyleColor(Color.clear);
            foldBtn.style.borderTopWidth = 0;
            foldBtn.style.borderBottomWidth = 0;
            foldBtn.style.borderLeftWidth = 0;
            foldBtn.style.borderRightWidth = 0;
            
            // 使用 KitIcons 的箭头图标
            string arrowIcon = isCollapsed ? KitIcons.CHEVRON_RIGHT : KitIcons.CHEVRON_DOWN;
            var arrowImg = new Image { image = KitIcons.GetTexture(arrowIcon) };
            arrowImg.style.width = 12;
            arrowImg.style.height = 12;
            arrowImg.tintColor = new Color(0.7f, 0.7f, 0.7f);
            foldBtn.Add(arrowImg);
        }

        /// <summary>
        /// 添加状态图标（错误/警告）
        /// </summary>
        private void AddStatusIcon(VisualElement card, bool hasError, bool hasWarning, BindTreeNode node)
        {
            if (hasError)
            {
                var errorIcon = new Image { image = KitIcons.GetTexture(KitIcons.ERROR) };
                errorIcon.style.width = 14;
                errorIcon.style.height = 14;
                errorIcon.style.marginRight = 6;
                errorIcon.tooltip = GetValidationTooltip(node);
                card.Add(errorIcon);
            }
            else if (hasWarning)
            {
                var warnIcon = new Image { image = KitIcons.GetTexture(KitIcons.WARNING) };
                warnIcon.style.width = 14;
                warnIcon.style.height = 14;
                warnIcon.style.marginRight = 6;
                warnIcon.tooltip = GetValidationTooltip(node);
                card.Add(warnIcon);
            }
        }
        
        /// <summary>
        /// 注册卡片点击事件
        /// </summary>
        private void RegisterCardClickHandler(VisualElement card, BindTreeNode node, bool hasError, Color bgColor)
        {
            card.RegisterCallback<ClickEvent>(evt =>
            {
                if (node.GameObject != null)
                {
                    Selection.activeGameObject = node.GameObject;
                    EditorGUIUtility.PingObject(node.GameObject);
                }
                evt.StopPropagation();
            });
            
            // 悬停效果
            Color hoverColor = hasError 
                ? new Color(0.45f, 0.18f, 0.18f) 
                : new Color(0.28f, 0.28f, 0.28f);
            card.RegisterCallback<MouseEnterEvent>(_ => card.style.backgroundColor = new StyleColor(hoverColor));
            card.RegisterCallback<MouseLeaveEvent>(_ => card.style.backgroundColor = new StyleColor(bgColor));
        }
        
        /// <summary>
        /// 获取绑定类型对应的颜色
        /// </summary>
        private Color GetBindTypeColor(BindType type) => type switch
        {
            BindType.Member => COLOR_MEMBER,
            BindType.Element => COLOR_ELEMENT,
            BindType.Component => COLOR_COMPONENT,
            BindType.Leaf => COLOR_LEAF,
            _ => COLOR_LEAF
        };

        /// <summary>
        /// 获取绑定类型对应的图标 ID
        /// </summary>
        private string GetBindTypeIconId(BindType type) => type switch
        {
            BindType.Member => KitIcons.DIAMOND,
            BindType.Element => KitIcons.DOT_FILLED,
            BindType.Component => KitIcons.DOT_FILLED,
            BindType.Leaf => KitIcons.DOT_EMPTY,
            _ => KitIcons.DOT
        };
        
        /// <summary>
        /// 获取类型的短名称
        /// </summary>
        private static string GetShortTypeName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
                return null;

            int lastDot = fullTypeName.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < fullTypeName.Length - 1)
                return fullTypeName.Substring(lastDot + 1);

            return fullTypeName;
        }
    }
}
#endif
