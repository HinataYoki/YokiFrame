#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - 区块/分组组件
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region 区块容器

        /// <summary>
        /// 创建带标题的区块容器
        /// </summary>
        /// <param name="title">区块标题</param>
        /// <param name="collapsible">是否可折叠</param>
        /// <returns>区块容器</returns>
        public static VisualElement CreateSection(string title, bool collapsible = false)
        {
            if (string.IsNullOrEmpty(title))
                title = "Untitled";

            var section = new VisualElement();
            section.style.marginBottom = Spacing.MD;
            section.style.paddingTop = Spacing.SM;
            section.style.paddingBottom = Spacing.SM;
            section.style.paddingLeft = Spacing.SM;
            section.style.paddingRight = Spacing.SM;
            section.style.backgroundColor = new StyleColor(Colors.LayerCard);
            section.style.borderTopLeftRadius = Radius.LG;
            section.style.borderTopRightRadius = Radius.LG;
            section.style.borderBottomLeftRadius = Radius.LG;
            section.style.borderBottomRightRadius = Radius.LG;

            if (collapsible)
            {
                var foldout = new Foldout { text = title, value = true };
                foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
                section.Add(foldout);
            }
            else
            {
                var header = CreateSectionHeader(title);
                section.Add(header);
            }

            return section;
        }

        /// <summary>
        /// 创建带图标的区块标题行
        /// </summary>
        /// <param name="title">标题文本</param>
        /// <param name="iconClass">图标 USS 类名（可选）</param>
        /// <returns>标题行元素</returns>
        public static VisualElement CreateSectionHeader(string title, string iconClass = null)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = Spacing.SM;

            if (!string.IsNullOrEmpty(iconClass))
            {
                var icon = new VisualElement();
                icon.AddToClassList(iconClass);
                icon.style.width = 16;
                icon.style.height = 16;
                icon.style.marginRight = Spacing.XS;
                header.Add(icon);
            }

            var label = new Label(title);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 13;
            label.style.color = new StyleColor(Colors.TextPrimary);
            header.Add(label);

            return header;
        }

        #endregion

        #region 布局容器

        /// <summary>
        /// 创建两列布局容器
        /// </summary>
        /// <returns>容器、左列、右列的元组</returns>
        public static (VisualElement container, VisualElement left, VisualElement right) CreateTwoColumnLayout()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexWrap = Wrap.Wrap;

            var left = new VisualElement();
            left.style.flexGrow = 1;
            left.style.flexBasis = new StyleLength(new Length(50, LengthUnit.Percent));
            left.style.minWidth = 200;

            var right = new VisualElement();
            right.style.flexGrow = 1;
            right.style.flexBasis = new StyleLength(new Length(50, LengthUnit.Percent));
            right.style.minWidth = 200;

            container.Add(left);
            container.Add(right);

            return (container, left, right);
        }

        /// <summary>
        /// 创建内容容器（带内边距）
        /// </summary>
        /// <returns>内容容器</returns>
        public static VisualElement CreateContentContainer()
        {
            var content = new VisualElement();
            content.style.paddingLeft = Spacing.MD;
            content.style.paddingRight = Spacing.MD;
            content.style.paddingTop = Spacing.SM;
            content.style.paddingBottom = Spacing.SM;
            return content;
        }

        #endregion

        #region 信息区域

        /// <summary>
        /// 创建信息页脚（带左边框的提示区域）
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="message">消息内容</param>
        /// <param name="accentColor">强调色（可选，默认为品牌主色）</param>
        /// <returns>信息页脚元素</returns>
        public static VisualElement CreateInfoFooter(string title, string message, Color? accentColor = null)
        {
            var color = accentColor ?? Colors.BrandPrimary;

            var footer = new VisualElement();
            footer.style.backgroundColor = new StyleColor(new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.8f));
            footer.style.borderLeftWidth = 3;
            footer.style.borderLeftColor = new StyleColor(color);
            footer.style.paddingLeft = Spacing.MD;
            footer.style.paddingRight = Spacing.MD;
            footer.style.paddingTop = Spacing.SM;
            footer.style.paddingBottom = Spacing.SM;
            footer.style.borderTopLeftRadius = Radius.MD;
            footer.style.borderTopRightRadius = Radius.MD;
            footer.style.borderBottomLeftRadius = Radius.MD;
            footer.style.borderBottomRightRadius = Radius.MD;

            // 标题行
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = Spacing.XS;

            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 12;
            titleLabel.style.color = new StyleColor(Colors.TextPrimary);
            titleRow.Add(titleLabel);

            footer.Add(titleRow);

            // 消息内容
            var messageLabel = new Label(message);
            messageLabel.style.fontSize = 11;
            messageLabel.style.color = new StyleColor(Colors.TextSecondary);
            messageLabel.style.whiteSpace = WhiteSpace.Normal;
            footer.Add(messageLabel);

            return footer;
        }

        /// <summary>
        /// 创建帮助提示框
        /// </summary>
        /// <param name="message">提示消息</param>
        /// <param name="type">提示类型</param>
        /// <returns>帮助提示框元素</returns>
        public static VisualElement CreateHelpBox(string message, HelpBoxType type = HelpBoxType.Info)
        {
            var color = type switch
            {
                HelpBoxType.Info => Colors.BrandPrimary,
                HelpBoxType.Warning => Colors.BrandWarning,
                HelpBoxType.Error => Colors.BrandDanger,
                HelpBoxType.Success => Colors.BrandSuccess,
                _ => Colors.BrandPrimary
            };

            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.FlexStart;
            box.style.backgroundColor = new StyleColor(new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f, 0.5f));
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = new StyleColor(color);
            box.style.paddingLeft = Spacing.SM;
            box.style.paddingRight = Spacing.SM;
            box.style.paddingTop = Spacing.XS;
            box.style.paddingBottom = Spacing.XS;
            box.style.marginTop = Spacing.XS;
            box.style.marginBottom = Spacing.XS;
            box.style.borderTopLeftRadius = Radius.SM;
            box.style.borderTopRightRadius = Radius.SM;
            box.style.borderBottomLeftRadius = Radius.SM;
            box.style.borderBottomRightRadius = Radius.SM;

            var label = new Label(message);
            label.style.fontSize = 11;
            label.style.color = new StyleColor(Colors.TextSecondary);
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);

            return box;
        }

        /// <summary>
        /// 帮助提示框类型
        /// </summary>
        public enum HelpBoxType
        {
            Info,
            Warning,
            Error,
            Success
        }

        #endregion
    }
}
#endif
