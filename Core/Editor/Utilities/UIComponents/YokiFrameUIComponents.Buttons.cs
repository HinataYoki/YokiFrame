#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - 按钮
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region 主要按钮

        /// <summary>
        /// 创建主按钮（品牌色填充）
        /// </summary>
        public static Button CreatePrimaryButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("action-button");
            button.AddToClassList("primary");
            return button;
        }

        /// <summary>
        /// 创建次要按钮（边框样式）
        /// </summary>
        public static Button CreateSecondaryButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("action-button");
            return button;
        }

        /// <summary>
        /// 创建危险按钮（红色）
        /// </summary>
        public static Button CreateDangerButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("action-button");
            button.AddToClassList("danger");
            return button;
        }

        #endregion

        #region 工具栏按钮

        /// <summary>
        /// 创建工具栏主按钮
        /// </summary>
        public static Button CreateToolbarPrimaryButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("toolbar-button");
            button.AddToClassList("primary");
            return button;
        }

        /// <summary>
        /// 创建工具栏次要按钮
        /// </summary>
        public static Button CreateToolbarButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("toolbar-button");
            return button;
        }

        #endregion

        #region 小型按钮

        /// <summary>
        /// 创建小型操作按钮
        /// </summary>
        public static Button CreateSmallButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.height = 20;
            btn.style.paddingLeft = Spacing.SM;
            btn.style.paddingRight = Spacing.SM;
            btn.style.marginLeft = Spacing.XS;
            btn.style.fontSize = 11;
            return btn;
        }

        #endregion

        #region 标签页按钮

        /// <summary>
        /// 创建标签页按钮
        /// </summary>
        public static Button CreateTabButton(string text, bool isActive, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.paddingLeft = Spacing.LG;
            btn.style.paddingRight = Spacing.LG;
            btn.style.paddingTop = 10;
            btn.style.paddingBottom = 10;
            btn.style.borderLeftWidth = btn.style.borderRightWidth = btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 2;
            btn.style.borderBottomColor = new StyleColor(isActive ? Colors.BrandPrimary : Color.clear);
            btn.style.backgroundColor = new StyleColor(isActive ? new Color(0.18f, 0.18f, 0.20f) : Color.clear);
            btn.style.color = new StyleColor(isActive ? Colors.TextPrimary : Colors.TextTertiary);
            return btn;
        }

        /// <summary>
        /// 更新标签页按钮样式
        /// </summary>
        public static void UpdateTabButtonStyle(Button btn, bool isActive)
        {
            btn.style.borderBottomColor = new StyleColor(isActive ? Colors.BrandPrimary : Color.clear);
            btn.style.color = new StyleColor(isActive ? Colors.TextPrimary : Colors.TextTertiary);
            btn.style.backgroundColor = new StyleColor(isActive ? new Color(0.18f, 0.18f, 0.20f) : Color.clear);
        }

        #endregion

        #region 切换按钮

        /// <summary>
        /// 创建过滤切换按钮（可切换激活状态）
        /// </summary>
        public static Button CreateFilterToggleButton(string text, bool initialValue, Color activeColor, Action<bool> onChanged)
        {
            var btn = new Button();
            btn.text = text;
            btn.style.height = 24;
            btn.style.marginRight = Spacing.XS;

            bool currentValue = initialValue;

            void UpdateStyle(bool isActive)
            {
                btn.style.backgroundColor = new StyleColor(isActive ? activeColor : new Color(0.2f, 0.2f, 0.2f));
                btn.style.color = new StyleColor(isActive ? Color.white : Colors.TextSecondary);
            }

            UpdateStyle(initialValue);

            btn.clicked += () =>
            {
                currentValue = !currentValue;
                UpdateStyle(currentValue);
                onChanged?.Invoke(currentValue);
            };

            return btn;
        }

        #endregion
    }
}
#endif
