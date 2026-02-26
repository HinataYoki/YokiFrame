#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiToolPageBase - UI 组件创建
    /// </summary>
    public abstract partial class YokiToolPageBase
    {
        #region 工具栏

        /// <summary>
        /// 创建工具栏容器
        /// </summary>
        protected VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");
            return toolbar;
        }

        /// <summary>
        /// 创建工具栏主按钮（品牌色填充）
        /// </summary>
        protected Button CreateToolbarPrimaryButton(string text, Action onClick)
            => YokiFrameUIComponents.CreateToolbarPrimaryButton(text, onClick);

        /// <summary>
        /// 创建工具栏次要按钮
        /// </summary>
        protected Button CreateToolbarButton(string text, Action onClick)
            => YokiFrameUIComponents.CreateToolbarButton(text, onClick);

        /// <summary>
        /// 创建工具栏 Toggle
        /// </summary>
        protected VisualElement CreateToolbarToggle(string text, bool value, Action<bool> onChanged)
        {
            var container = new VisualElement();
            container.AddToClassList("toolbar-toggle");
            if (value) container.AddToClassList("checked");

            var label = new Label(text);
            label.AddToClassList("toolbar-toggle-label");
            container.Add(label);

            container.RegisterCallback<ClickEvent>(evt =>
            {
                bool isChecked = container.ClassListContains("checked");
                if (isChecked)
                    container.RemoveFromClassList("checked");
                else
                    container.AddToClassList("checked");
                onChanged?.Invoke(!isChecked);
            });

            return container;
        }

        /// <summary>
        /// 创建工具栏弹性空间
        /// </summary>
        protected VisualElement CreateToolbarSpacer()
        {
            var spacer = new VisualElement();
            spacer.AddToClassList("toolbar-spacer");
            return spacer;
        }

        #endregion

        #region 卡片与布局

        /// <summary>
        /// 创建现代化卡片
        /// </summary>
        protected (VisualElement card, VisualElement body) CreateCard(string title = null, string icon = null)
            => YokiFrameUIComponents.CreateCard(title, icon);

        /// <summary>
        /// 创建分割面板
        /// </summary>
        protected TwoPaneSplitView CreateSplitView(float initialLeftWidth = 280f)
        {
            var splitView = new TwoPaneSplitView(0, initialLeftWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.AddToClassList("split-view");
            splitView.style.flexGrow = 1;
            return splitView;
        }

        /// <summary>
        /// 创建分割面板（带 EditorPrefs 持久化）
        /// </summary>
        protected TwoPaneSplitView CreateSplitView(float initialLeftWidth, string prefsKey)
        {
            float savedWidth = string.IsNullOrEmpty(prefsKey)
                ? initialLeftWidth
                : EditorPrefs.GetFloat(prefsKey, initialLeftWidth);

            var splitView = new TwoPaneSplitView(0, savedWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.AddToClassList("split-view");
            splitView.style.flexGrow = 1;

            if (!string.IsNullOrEmpty(prefsKey))
            {
                // 使用闭包捕获变量（RegisterCallback 不支持带 context 的静态 lambda）
                string capturedKey = prefsKey;
                splitView.RegisterCallback<GeometryChangedEvent>(evt =>
                {
                    float currentWidth = splitView.fixedPane?.resolvedStyle.width ?? 0;
                    if (currentWidth > 0)
                    {
                        EditorPrefs.SetFloat(capturedKey, currentWidth);
                    }
                });
            }

            return splitView;
        }

        /// <summary>
        /// 创建面板头部
        /// </summary>
        protected VisualElement CreatePanelHeader(string title)
        {
            var header = new VisualElement();
            header.AddToClassList("panel-header");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("panel-title");
            header.Add(titleLabel);

            return header;
        }

        #endregion

        #region 表单与信息

        /// <summary>
        /// 创建现代化 Toggle 开关
        /// </summary>
        protected VisualElement CreateModernToggle(string label, bool value, Action<bool> onChanged)
            => YokiFrameUIComponents.CreateModernToggle(label, value, onChanged);

        /// <summary>
        /// 创建信息行
        /// </summary>
        protected (VisualElement row, Label valueLabel) CreateInfoRow(string label, string initialValue = "-")
            => YokiFrameUIComponents.CreateInfoRow(label, initialValue);

        /// <summary>
        /// 创建整数配置行
        /// </summary>
        protected (VisualElement row, TextField field) CreateIntConfigRow(
            string label, int value, Action<int> onChanged, int minValue = int.MinValue)
            => YokiFrameUIComponents.CreateIntConfigRow(label, value, onChanged, minValue);

        #endregion

        #region 按钮

        /// <summary>
        /// 创建主按钮
        /// </summary>
        protected Button CreatePrimaryButton(string text, Action onClick)
            => YokiFrameUIComponents.CreatePrimaryButton(text, onClick);

        /// <summary>
        /// 创建次要按钮
        /// </summary>
        protected Button CreateSecondaryButton(string text, Action onClick)
            => YokiFrameUIComponents.CreateSecondaryButton(text, onClick);

        /// <summary>
        /// 创建危险按钮
        /// </summary>
        protected Button CreateDangerButton(string text, Action onClick)
            => YokiFrameUIComponents.CreateDangerButton(text, onClick);

        /// <summary>
        /// 创建带图标的工具栏按钮
        /// </summary>
        protected Button CreateToolbarButtonWithIcon(string iconId, string text, Action onClick)
            => YokiFrameUIComponents.CreateToolbarButtonWithIcon(iconId, text, onClick);

        /// <summary>
        /// 创建带图标的操作按钮
        /// </summary>
        protected Button CreateActionButtonWithIcon(string iconId, string text, Action onClick, bool isDanger = false)
            => YokiFrameUIComponents.CreateActionButtonWithIcon(iconId, text, onClick, isDanger);

        #endregion

        #region 提示与状态

        /// <summary>
        /// 创建帮助框
        /// </summary>
        protected VisualElement CreateHelpBox(string message)
            => YokiFrameUIComponents.CreateHelpBox(message);

        /// <summary>
        /// 创建帮助框（带类型）
        /// </summary>
        protected VisualElement CreateHelpBox(string message, YokiFrameUIComponents.HelpBoxType type)
            => YokiFrameUIComponents.CreateHelpBox(message, type);

        /// <summary>
        /// 创建空状态提示
        /// </summary>
        protected VisualElement CreateEmptyState(string message)
            => YokiFrameUIComponents.CreateEmptyState(KitIcons.INFO, message);

        /// <summary>
        /// 创建空状态提示（带图标和提示）
        /// </summary>
        protected VisualElement CreateEmptyState(string icon, string message, string hint = null)
            => YokiFrameUIComponents.CreateEmptyState(icon, message, hint);

        /// <summary>
        /// 创建分隔线
        /// </summary>
        protected VisualElement CreateDivider()
            => YokiFrameUIComponents.CreateDivider();

        #endregion

        #region 动画

        /// <summary>
        /// 为元素添加淡入动画
        /// </summary>
        protected void AddFadeInAnimation(VisualElement element, int delayMs = 0)
            => YokiFrameUIComponents.AddFadeInAnimation(element, delayMs);

        /// <summary>
        /// 为元素添加滑入动画
        /// </summary>
        protected void AddSlideInAnimation(VisualElement element, YokiFrameUIComponents.SlideDirection direction, int delayMs = 0)
            => YokiFrameUIComponents.AddSlideInAnimation(element, direction, delayMs);

        #endregion
    }
}
#endif
/// 