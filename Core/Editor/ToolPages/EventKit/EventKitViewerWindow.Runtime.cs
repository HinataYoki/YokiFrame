#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 查看器 - 运行时视图
    /// </summary>
    public partial class EventKitViewerWindow
    {
        #region 运行时视图构建

        /// <summary>
        /// 构建运行时视图
        /// </summary>
        private void BuildRuntimeView()
        {
            mRuntimeView = new VisualElement();
            mRuntimeView.style.flexGrow = 1;
            mRuntimeView.style.flexDirection = FlexDirection.Column;
            mRuntimeView.style.display = DisplayStyle.None;
            mContentContainer.Add(mRuntimeView);

            BuildRuntimeToolbar();
            BuildRuntimeContent();
        }

        /// <summary>
        /// 构建运行时工具栏
        /// </summary>
        private void BuildRuntimeToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 28;
            toolbar.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.alignItems = Align.Center;
            mRuntimeView.Add(toolbar);

            var label = new Label("事件类型:");
            label.style.marginRight = 8;
            label.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            toolbar.Add(label);

            // 类别按钮
            mEnumCategoryBtn = CreateCategoryButton("Enum", () => SwitchCategory(EventCategory.Enum));
            mTypeCategoryBtn = CreateCategoryButton("Type", () => SwitchCategory(EventCategory.Type));
            mStringCategoryBtn = CreateCategoryButton("String", () => SwitchCategory(EventCategory.String));

            toolbar.Add(mEnumCategoryBtn);
            toolbar.Add(mTypeCategoryBtn);
            toolbar.Add(mStringCategoryBtn);

            // 弹性空间
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            // 刷新按钮
            var refreshBtn = new Button(RefreshEventData) { text = "刷新" };
            refreshBtn.style.height = 22;
            refreshBtn.style.paddingLeft = 12;
            refreshBtn.style.paddingRight = 12;
            toolbar.Add(refreshBtn);

            UpdateCategoryButtons();
        }

        /// <summary>
        /// 构建运行时内容区域
        /// </summary>
        private void BuildRuntimeContent()
        {
            var content = new VisualElement();
            content.style.flexGrow = 1;
            content.style.flexDirection = FlexDirection.Row;
            content.style.paddingTop = 8;
            content.style.paddingBottom = 8;
            content.style.paddingLeft = 8;
            content.style.paddingRight = 8;
            mRuntimeView.Add(content);

            BuildEventListPanel(content);
            BuildListenerDetailPanel(content);
        }

        /// <summary>
        /// 构建事件列表面板
        /// </summary>
        private void BuildEventListPanel(VisualElement parent)
        {
            var panel = new VisualElement();
            panel.style.width = 280;
            panel.style.marginRight = 8;
            panel.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            panel.style.borderTopLeftRadius = 4;
            panel.style.borderTopRightRadius = 4;
            panel.style.borderBottomLeftRadius = 4;
            panel.style.borderBottomRightRadius = 4;
            parent.Add(panel);

            // 标题
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            panel.Add(header);

            var titleLabel = new Label("已注册事件");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            header.Add(titleLabel);

            mEventCountLabel = new Label("(0)");
            mEventCountLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            header.Add(mEventCountLabel);

            // 事件列表
            mEventListView = new ListView();
            mEventListView.fixedItemHeight = 28;
            mEventListView.makeItem = MakeEventItem;
            mEventListView.bindItem = BindEventItem;
#if UNITY_2022_1_OR_NEWER
            mEventListView.selectionChanged += OnEventSelected;
#else
            mEventListView.onSelectionChange += OnEventSelected;
#endif
            mEventListView.style.flexGrow = 1;
            panel.Add(mEventListView);
        }

        /// <summary>
        /// 构建监听器详情面板
        /// </summary>
        private void BuildListenerDetailPanel(VisualElement parent)
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;
            panel.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            panel.style.borderTopLeftRadius = 4;
            panel.style.borderTopRightRadius = 4;
            panel.style.borderBottomLeftRadius = 4;
            panel.style.borderBottomRightRadius = 4;
            parent.Add(panel);

            // 标题
            var header = new VisualElement();
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            panel.Add(header);

            var titleLabel = new Label("监听器详情");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            header.Add(titleLabel);

            // 详情容器
            mListenerDetailContainer = new ScrollView(ScrollViewMode.Vertical);
            mListenerDetailContainer.style.flexGrow = 1;
            mListenerDetailContainer.style.paddingTop = 8;
            mListenerDetailContainer.style.paddingBottom = 8;
            mListenerDetailContainer.style.paddingLeft = 12;
            mListenerDetailContainer.style.paddingRight = 12;
            panel.Add(mListenerDetailContainer);

            // 默认提示
            var hint = new Label("选择左侧事件查看监听器详情");
            hint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginTop = 50;
            mListenerDetailContainer.Add(hint);
        }

        #endregion

        #region 类别切换

        /// <summary>
        /// 切换事件类别
        /// </summary>
        private void SwitchCategory(EventCategory category)
        {
            mSelectedCategory = category;
            mSelectedEventKey = null;
            UpdateCategoryButtons();
            RefreshEventData();
        }

        /// <summary>
        /// 更新类别按钮状态
        /// </summary>
        private void UpdateCategoryButtons()
        {
            if (mEnumCategoryBtn == null) return;

            UpdateCategoryButtonState(mEnumCategoryBtn, mSelectedCategory == EventCategory.Enum);
            UpdateCategoryButtonState(mTypeCategoryBtn, mSelectedCategory == EventCategory.Type);
            UpdateCategoryButtonState(mStringCategoryBtn, mSelectedCategory == EventCategory.String);
        }

        #endregion
    }
}
#endif
