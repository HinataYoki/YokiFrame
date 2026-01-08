#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - 可折叠面板
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region 可折叠面板

        /// <summary>
        /// 创建可折叠详情面板（初始隐藏）
        /// </summary>
        /// <param name="onToggle">展开/折叠回调</param>
        /// <returns>容器和内容区域</returns>
        public static (VisualElement container, VisualElement content) CreateFoldoutPanel(Action<bool> onToggle = null)
        {
            var container = new VisualElement();
            container.name = "foldout-panel";
            container.style.display = DisplayStyle.None;
            container.style.backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.16f));
            container.style.marginLeft = 40;
            container.style.marginRight = 40;
            container.style.marginTop = 4;
            container.style.marginBottom = 8;
            container.style.paddingLeft = Spacing.MD;
            container.style.paddingRight = Spacing.MD;
            container.style.paddingTop = Spacing.SM;
            container.style.paddingBottom = Spacing.SM;
            container.style.borderTopLeftRadius = Radius.MD;
            container.style.borderTopRightRadius = Radius.MD;
            container.style.borderBottomLeftRadius = Radius.MD;
            container.style.borderBottomRightRadius = Radius.MD;
            container.style.borderLeftWidth = 2;
            container.style.borderLeftColor = new StyleColor(Colors.BrandPrimary);

            var content = new VisualElement();
            container.Add(content);

            return (container, content);
        }

        /// <summary>
        /// 切换折叠面板显示状态
        /// </summary>
        public static void ToggleFoldoutPanel(VisualElement panel)
        {
            if (panel == null) return;
            var isVisible = panel.style.display == DisplayStyle.Flex;
            panel.style.display = isVisible ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>
        /// 创建详情列表区块（带标题）
        /// </summary>
        /// <param name="title">区块标题</param>
        /// <param name="icon">图标</param>
        /// <param name="accentColor">强调色</param>
        public static (VisualElement section, VisualElement list) CreateDetailSection(
            string title, 
            string icon, 
            Color accentColor)
        {
            var section = new VisualElement();
            section.style.marginBottom = Spacing.SM;

            // 标题行
            var titleRow = CreateRow();
            titleRow.style.marginBottom = Spacing.XS;
            section.Add(titleRow);

            var titleLabel = new Label($"{icon} {title}");
            titleLabel.style.fontSize = 11;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(accentColor);
            titleRow.Add(titleLabel);

            // 列表容器
            var list = new VisualElement();
            list.style.paddingLeft = Spacing.MD;
            section.Add(list);

            return (section, list);
        }

        /// <summary>
        /// 创建可点击的位置标签（用于详情面板）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="lineNumber">行号</param>
        /// <param name="textColor">文本颜色</param>
        /// <param name="onClick">点击回调</param>
        public static Label CreateClickableLocationLabel(
            string filePath, 
            int lineNumber, 
            Color textColor,
            Action onClick)
        {
            var fileName = System.IO.Path.GetFileName(filePath);
            var label = new Label($"  • {fileName}:{lineNumber}");
            label.style.fontSize = 10;
            label.style.color = new StyleColor(textColor);
            label.style.marginBottom = 2;
            // 设置鼠标指针样式为链接
            label.AddToClassList("clickable");

            // 悬停效果
            var hoverColor = new Color(
                Mathf.Min(textColor.r + 0.2f, 1f),
                Mathf.Min(textColor.g + 0.2f, 1f),
                Mathf.Min(textColor.b + 0.2f, 1f)
            );

            label.RegisterCallback<MouseEnterEvent>(_ => label.style.color = new StyleColor(hoverColor));
            label.RegisterCallback<MouseLeaveEvent>(_ => label.style.color = new StyleColor(textColor));
            label.RegisterCallback<ClickEvent>(_ => onClick?.Invoke());

            return label;
        }

        /// <summary>
        /// 创建统计徽章（如 R:3 U:2）
        /// </summary>
        public static VisualElement CreateStatsBadge(string label, int count, Color color)
        {
            var badge = new VisualElement();
            badge.style.flexDirection = FlexDirection.Row;
            badge.style.alignItems = Align.Center;
            badge.style.backgroundColor = new StyleColor(new Color(color.r, color.g, color.b, 0.2f));
            badge.style.paddingLeft = 6;
            badge.style.paddingRight = 6;
            badge.style.paddingTop = 2;
            badge.style.paddingBottom = 2;
            badge.style.borderTopLeftRadius = Radius.SM;
            badge.style.borderTopRightRadius = Radius.SM;
            badge.style.borderBottomLeftRadius = Radius.SM;
            badge.style.borderBottomRightRadius = Radius.SM;
            badge.style.marginRight = 4;

            var text = new Label($"{label}:{count}");
            text.style.fontSize = 9;
            text.style.color = new StyleColor(color);
            badge.Add(text);

            return badge;
        }

        #endregion

        #region 状态徽章

        /// <summary>
        /// 创建状态徽章（带背景色和文本色）
        /// </summary>
        /// <param name="text">徽章文本</param>
        /// <param name="bgColor">背景色</param>
        /// <param name="textColor">文本色</param>
        public static Label CreateStatusBadge(string text, Color bgColor, Color textColor)
        {
            var label = new Label(text);
            label.style.fontSize = 9;
            label.style.paddingLeft = 6;
            label.style.paddingRight = 6;
            label.style.paddingTop = 2;
            label.style.paddingBottom = 2;
            label.style.borderTopLeftRadius = Radius.MD;
            label.style.borderTopRightRadius = Radius.MD;
            label.style.borderBottomLeftRadius = Radius.MD;
            label.style.borderBottomRightRadius = Radius.MD;
            label.style.backgroundColor = new StyleColor(bgColor);
            label.style.color = new StyleColor(textColor);
            return label;
        }

        #endregion
    }
}
#endif
