#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    ///    ///  Toast 提示
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        /// <summary>
        /// 在指定容器中显示 Toast 提示（自动消失）
        /// </summary>
        /// <param name="container">父容器（Toast 会添加到此容器）</param>
        /// <param name="message">提示消息</param>
        /// <param name="durationMs">显示时长（毫秒），默认 1500ms</param>
        /// <param name="iconId">图标 ID（可选）</param>
        public static void ShowToast(VisualElement container, string message, int durationMs = 1500, string iconId = null)
        {
            if (container == default) return;

            var toast = CreateToastElement(message, iconId);
            container.Add(toast);

            // 淡入动画
            toast.schedule.Execute(() =>
            {
                toast.style.opacity = 1;
                toast.style.translate = new Translate(Length.Percent(-50), 0);
            }).ExecuteLater(10);

            // 淡出并移除
            toast.schedule.Execute(() =>
            {
                toast.style.opacity = 0;
                toast.style.translate = new Translate(Length.Percent(-50), -10);
            }).ExecuteLater(durationMs);

            toast.schedule.Execute(() => toast.RemoveFromHierarchy()).ExecuteLater(durationMs + 300);
        }

        /// <summary>
        /// 在指定元素附近显示 Toast 提示
        /// </summary>
        /// <param name="anchor">锚点元素</param>
        /// <param name="message">提示消息</param>
        /// <param name="durationMs">显示时长（毫秒）</param>
        public static void ShowToastNear(VisualElement anchor, string message, int durationMs = 1500)
        {
            if (anchor == default) return;

            // 查找根容器
            var root = anchor.panel?.visualTree;
            if (root == default) return;

            var toast = CreateToastElement(message, KitIcons.SUCCESS);
            toast.style.position = Position.Absolute;

            // 计算位置（在锚点上方）
            var anchorRect = anchor.worldBound;
            toast.style.left = anchorRect.x + anchorRect.width / 2;
            toast.style.top = anchorRect.y - 40;

            root.Add(toast);

            // 淡入动画
            toast.schedule.Execute(() =>
            {
                toast.style.opacity = 1;
                toast.style.translate = new Translate(Length.Percent(-50), 0);
            }).ExecuteLater(10);

            // 淡出并移除
            toast.schedule.Execute(() =>
            {
                toast.style.opacity = 0;
                toast.style.translate = new Translate(Length.Percent(-50), -10);
            }).ExecuteLater(durationMs);

            toast.schedule.Execute(() => toast.RemoveFromHierarchy()).ExecuteLater(durationMs + 300);
        }

        /// <summary>
        /// 创建 Toast 元素
        /// </summary>
        private static VisualElement CreateToastElement(string message, string iconId)
        {
            var toast = new VisualElement();
            toast.style.position = Position.Absolute;
            toast.style.left = Length.Percent(50);
            toast.style.top = 20;
            toast.style.flexDirection = FlexDirection.Row;
            toast.style.alignItems = Align.Center;
            toast.style.paddingLeft = 16;
            toast.style.paddingRight = 16;
            toast.style.paddingTop = 10;
            toast.style.paddingBottom = 10;
            toast.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.18f, 0.95f));
            toast.style.borderTopLeftRadius = 8;
            toast.style.borderTopRightRadius = 8;
            toast.style.borderBottomLeftRadius = 8;
            toast.style.borderBottomRightRadius = 8;
            toast.style.borderLeftWidth = 1;
            toast.style.borderRightWidth = 1;
            toast.style.borderTopWidth = 1;
            toast.style.borderBottomWidth = 1;
            toast.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.1f));
            toast.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.1f));
            toast.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.1f));
            toast.style.borderBottomColor = new StyleColor(new Color(1f, 1f, 1f, 0.1f));
            toast.style.opacity = 0;
            toast.style.translate = new Translate(Length.Percent(-50), -10);
            toast.pickingMode = PickingMode.Ignore;

            // 过渡动画
            toast.style.transitionProperty = new List<StylePropertyName>
            {
                new("opacity"), new("translate")
            };
            toast.style.transitionDuration = new List<TimeValue>
            {
                new(200, TimeUnit.Millisecond), new(200, TimeUnit.Millisecond)
            };
            toast.style.transitionTimingFunction = new List<EasingFunction>
            {
                new(EasingMode.EaseOut), new(EasingMode.EaseOut)
            };

            // 图标
            if (!string.IsNullOrEmpty(iconId))
            {
                var icon = new Image { image = KitIcons.GetTexture(iconId) };
                icon.style.width = 16;
                icon.style.height = 16;
                icon.style.marginRight = 8;
                icon.tintColor = Colors.StatusSuccess;
                toast.Add(icon);
            }

            // 消息文本
            var label = new Label(message);
            label.style.fontSize = 13;
            label.style.color = new StyleColor(Colors.TextPrimary);
            toast.Add(label);

            return toast;
        }
    }
}
#endif