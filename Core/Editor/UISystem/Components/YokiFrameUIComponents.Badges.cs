#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - 徽章/标签组件
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region 徽章

        /// <summary>
        /// 创建统计徽章
        /// </summary>
        /// <param name="label">标签文本</param>
        /// <param name="value">数值</param>
        /// <param name="color">背景颜色</param>
        /// <returns>徽章元素</returns>
        public static VisualElement CreateBadge(string label, int value, Color color)
        {
            var badge = new VisualElement();
            badge.style.flexDirection = FlexDirection.Row;
            badge.style.alignItems = Align.Center;
            badge.style.paddingLeft = Spacing.SM;
            badge.style.paddingRight = Spacing.SM;
            badge.style.paddingTop = Spacing.XS;
            badge.style.paddingBottom = Spacing.XS;
            badge.style.backgroundColor = new StyleColor(color);
            badge.style.borderTopLeftRadius = Radius.MD;
            badge.style.borderTopRightRadius = Radius.MD;
            badge.style.borderBottomLeftRadius = Radius.MD;
            badge.style.borderBottomRightRadius = Radius.MD;
            badge.style.marginRight = Spacing.SM;

            var labelElement = new Label(label);
            labelElement.style.fontSize = 11;
            labelElement.style.color = new StyleColor(Colors.TextSecondary);
            labelElement.style.marginRight = Spacing.XS;
            badge.Add(labelElement);

            var valueElement = new Label(value.ToString());
            valueElement.style.fontSize = 12;
            valueElement.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueElement.style.color = new StyleColor(Colors.TextPrimary);
            badge.Add(valueElement);

            return badge;
        }

        /// <summary>
        /// 创建统计徽章（使用预设颜色）
        /// </summary>
        /// <param name="label">标签文本</param>
        /// <param name="value">数值</param>
        /// <param name="type">徽章类型</param>
        /// <returns>徽章元素</returns>
        public static VisualElement CreateBadge(string label, int value, BadgeType type = BadgeType.Default)
        {
            var color = type switch
            {
                BadgeType.Success => Colors.BadgeSuccess,
                BadgeType.Warning => Colors.BadgeWarning,
                BadgeType.Error => Colors.BadgeError,
                BadgeType.Info => Colors.BadgeInfo,
                _ => Colors.BadgeDefault
            };
            return CreateBadge(label, value, color);
        }

        /// <summary>
        /// 徽章类型
        /// </summary>
        public enum BadgeType
        {
            Default,
            Success,
            Warning,
            Error,
            Info
        }

        #endregion

        #region 带图标的标签

        /// <summary>
        /// 创建带图标的标签
        /// </summary>
        /// <param name="text">文本</param>
        /// <param name="iconClass">图标 USS 类名</param>
        /// <returns>标签元素</returns>
        public static VisualElement CreateIconLabel(string text, string iconClass)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            if (!string.IsNullOrEmpty(iconClass))
            {
                var icon = new VisualElement();
                icon.AddToClassList(iconClass);
                icon.style.width = 14;
                icon.style.height = 14;
                icon.style.marginRight = Spacing.XS;
                container.Add(icon);
            }

            var label = new Label(text);
            label.style.fontSize = 12;
            label.style.color = new StyleColor(Colors.TextPrimary);
            container.Add(label);

            return container;
        }

        /// <summary>
        /// 创建带图标的标签（使用 Unicode 图标）
        /// </summary>
        /// <param name="text">文本</param>
        /// <param name="iconChar">Unicode 图标字符</param>
        /// <returns>标签元素</returns>
        public static VisualElement CreateIconLabelWithChar(string text, string iconChar)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            if (!string.IsNullOrEmpty(iconChar))
            {
                var icon = new Label(iconChar);
                icon.style.fontSize = 12;
                icon.style.marginRight = Spacing.XS;
                container.Add(icon);
            }

            var label = new Label(text);
            label.style.fontSize = 12;
            label.style.color = new StyleColor(Colors.TextPrimary);
            container.Add(label);

            return container;
        }

        #endregion

        #region 状态指示器

        /// <summary>
        /// 创建状态指示器
        /// </summary>
        /// <param name="status">状态类型</param>
        /// <param name="text">状态文本</param>
        /// <returns>状态指示器元素</returns>
        public static VisualElement CreateStatusIndicator(StatusType status, string text)
        {
            var color = status switch
            {
                StatusType.Success => Colors.StatusSuccess,
                StatusType.Warning => Colors.StatusWarning,
                StatusType.Error => Colors.StatusError,
                _ => Colors.StatusInfo
            };

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            // 状态圆点
            var dot = new VisualElement();
            dot.style.width = 8;
            dot.style.height = 8;
            dot.style.borderTopLeftRadius = 4;
            dot.style.borderTopRightRadius = 4;
            dot.style.borderBottomLeftRadius = 4;
            dot.style.borderBottomRightRadius = 4;
            dot.style.backgroundColor = new StyleColor(color);
            dot.style.marginRight = Spacing.XS;
            container.Add(dot);

            // 状态文本
            var label = new Label(text);
            label.style.fontSize = 11;
            label.style.color = new StyleColor(color);
            container.Add(label);

            return container;
        }

        /// <summary>
        /// 状态类型
        /// </summary>
        public enum StatusType
        {
            Info,
            Success,
            Warning,
            Error
        }

        #endregion

        #region 计数标签

        /// <summary>
        /// 创建简单文本徽章
        /// </summary>
        /// <param name="text">文本</param>
        /// <param name="color">背景颜色</param>
        /// <returns>徽章元素</returns>
        public static VisualElement CreateBadge(string text, Color color)
        {
            var badge = new VisualElement();
            badge.style.paddingLeft = Spacing.SM;
            badge.style.paddingRight = Spacing.SM;
            badge.style.paddingTop = Spacing.XS;
            badge.style.paddingBottom = Spacing.XS;
            badge.style.backgroundColor = new StyleColor(color);
            badge.style.borderTopLeftRadius = Radius.MD;
            badge.style.borderTopRightRadius = Radius.MD;
            badge.style.borderBottomLeftRadius = Radius.MD;
            badge.style.borderBottomRightRadius = Radius.MD;

            var label = new Label(text);
            label.style.fontSize = 11;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new StyleColor(Colors.TextPrimary);
            badge.Add(label);

            return badge;
        }

        /// <summary>
        /// 创建计数标签（小型圆形徽章）
        /// </summary>
        /// <param name="count">数量</param>
        /// <param name="color">背景颜色</param>
        /// <returns>计数标签元素</returns>
        public static VisualElement CreateCountLabel(int count, Color color)
        {
            var badge = new VisualElement();
            badge.style.minWidth = 18;
            badge.style.height = 18;
            badge.style.paddingLeft = Spacing.XS;
            badge.style.paddingRight = Spacing.XS;
            badge.style.backgroundColor = new StyleColor(color);
            badge.style.borderTopLeftRadius = 9;
            badge.style.borderTopRightRadius = 9;
            badge.style.borderBottomLeftRadius = 9;
            badge.style.borderBottomRightRadius = 9;
            badge.style.alignItems = Align.Center;
            badge.style.justifyContent = Justify.Center;

            var label = new Label(count.ToString());
            label.style.fontSize = 10;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new StyleColor(Colors.TextPrimary);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.Add(label);

            return badge;
        }

        #endregion
    }
}
#endif
