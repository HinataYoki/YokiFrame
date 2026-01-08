#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - 配置表信息区块
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region F. 配置表信息区

        private TextField mTablesSearchField;
        private string mTablesSearchText = "";
        private object mCachedTables;

        private VisualElement BuildTablesInfo()
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(Design.LayerCard);
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 8;
            container.style.borderLeftWidth = container.style.borderRightWidth = 1;
            container.style.borderTopWidth = container.style.borderBottomWidth = 1;
            container.style.borderLeftColor = container.style.borderRightColor = new StyleColor(Design.BorderDefault);
            container.style.borderTopColor = container.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.style.marginBottom = 16;

            // 标题栏
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.Add(header);

            var title = new Label("配置表信息");
            title.style.fontSize = Design.FontSizeSection;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Design.TextPrimary);
            header.Add(title);

            // 右侧操作区：刷新缓存按钮 + 搜索框
            var rightContainer = new VisualElement();
            rightContainer.style.flexDirection = FlexDirection.Row;
            rightContainer.style.alignItems = Align.Center;
            header.Add(rightContainer);

            // 刷新缓存按钮
            var refreshBtn = new Button(RefreshEditorCache) { text = "刷新缓存" };
            ApplySmallButtonStyle(refreshBtn);
            refreshBtn.style.marginRight = 8;
            rightContainer.Add(refreshBtn);

            // 搜索框
            var searchContainer = new VisualElement();
            searchContainer.style.flexDirection = FlexDirection.Row;
            searchContainer.style.alignItems = Align.Center;
            rightContainer.Add(searchContainer);

            var searchIcon = new Label("[搜索]");
            searchIcon.style.marginRight = 4;
            searchIcon.style.fontSize = Design.FontSizeSmall;
            searchIcon.style.color = new StyleColor(Design.TextTertiary);
            searchContainer.Add(searchIcon);

            mTablesSearchField = new TextField();
            mTablesSearchField.style.width = 150;
            mTablesSearchField.style.height = 22;
            // 设置占位符样式
            var placeholder = "搜索表名...";
            mTablesSearchField.value = placeholder;
            mTablesSearchField.style.color = new StyleColor(Design.TextTertiary);

            mTablesSearchField.RegisterCallback<FocusInEvent>(_ =>
            {
                if (mTablesSearchField.value == placeholder)
                {
                    mTablesSearchField.value = "";
                    mTablesSearchField.style.color = new StyleColor(Design.TextPrimary);
                }
            });

            mTablesSearchField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (string.IsNullOrEmpty(mTablesSearchField.value))
                {
                    mTablesSearchField.value = placeholder;
                    mTablesSearchField.style.color = new StyleColor(Design.TextTertiary);
                }
            });

            mTablesSearchField.RegisterValueChangedCallback(evt =>
            {
                var newValue = evt.newValue;
                if (newValue == placeholder) newValue = "";

                mTablesSearchText = newValue;
                FilterTablesInfo();
            });
            searchContainer.Add(mTablesSearchField);

            mTablesInfoContainer = new VisualElement();
            mTablesInfoContainer.style.paddingLeft = 12;
            mTablesInfoContainer.style.paddingRight = 12;
            mTablesInfoContainer.style.paddingBottom = 12;
            mTablesInfoContainer.style.maxHeight = 300;
            container.Add(mTablesInfoContainer);

            // 使用 ScrollView 包裹内容
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            mTablesInfoContainer.Add(scrollView);

            var hint = new Label("点击「刷新缓存」加载配置表信息");
            hint.style.color = new StyleColor(Design.TextTertiary);
            hint.style.marginTop = 8;
            scrollView.Add(hint);

            return container;
        }

        /// <summary>
        /// 根据搜索文本过滤配置表信息
        /// </summary>
        private void FilterTablesInfo()
        {
            if (mCachedTables == null) return;
            RefreshTablesInfoInternal(mCachedTables, mTablesSearchText);
        }

        #endregion
    }
}
#endif
