#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - 资源监控 UI 构建
    /// 使用 USS 类消除内联样式
    /// </summary>
    public partial class ResKitToolPage
    {
        /// <summary>
        /// 构建资源监控 UI
        /// </summary>
        private void BuildResourceMonitorUI(VisualElement root)
        {
            root.Add(CreateMonitorToolbar());
            root.Add(CreateSearchBar());

            // 内容区域（填充剩余空间）
            var content = new VisualElement();
            content.AddToClassList("content-area");
            content.style.flexGrow = 1;
            root.Add(content);

            // 使用 TwoPaneSplitView 实现左右分割
            var splitView = CreateSplitView(300f);
            content.Add(splitView);

            splitView.Add(CreateLeftPanel());
            splitView.Add(CreateRightPanel());

            ShowEmptyState();
            RefreshHistoryDisplay();
        }

        /// <summary>
        /// 创建资源监控工具栏
        /// </summary>
        private VisualElement CreateMonitorToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");

            var refreshBtn = YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.REFRESH, "刷新", RefreshData);
            refreshBtn.AddToClassList("toolbar-button");
            toolbar.Add(refreshBtn);

            var expandAllBtn = YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.EXPAND, "全部展开", ExpandAllCategories);
            expandAllBtn.AddToClassList("toolbar-button");
            toolbar.Add(expandAllBtn);

            var collapseAllBtn = new Button(CollapseAllCategories) { text = "全部折叠" };
            collapseAllBtn.AddToClassList("toolbar-button");
            toolbar.Add(collapseAllBtn);

            var clearHistoryBtn = YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.DELETE, "清空历史", ClearHistory);
            clearHistoryBtn.AddToClassList("toolbar-button");
            toolbar.Add(clearHistoryBtn);

            var autoRefreshToggle = YokiFrameUIComponents.CreateModernToggle(
                "自动刷新",
                mAutoRefresh,
                value => mAutoRefresh = value
            );
            toolbar.Add(autoRefreshToggle);

            toolbar.Add(YokiFrameUIComponents.CreateFlexSpacer());

            mLoadedCountLabel = new Label("已加载: 0");
            mLoadedCountLabel.AddToClassList("toolbar-label");
            toolbar.Add(mLoadedCountLabel);

            mTotalRefCountLabel = new Label("总引用: 0");
            mTotalRefCountLabel.AddToClassList("toolbar-label");
            toolbar.Add(mTotalRefCountLabel);

            return toolbar;
        }

        /// <summary>
        /// 创建搜索栏
        /// </summary>
        private VisualElement CreateSearchBar()
        {
            var searchBar = new VisualElement();
            searchBar.AddToClassList("yoki-res-search");

            var searchIcon = new Image { image = KitIcons.GetTexture(KitIcons.TARGET) };
            searchIcon.AddToClassList("yoki-res-search__icon");
            searchBar.Add(searchIcon);

            mSearchField = new TextField();
            mSearchField.AddToClassList("yoki-res-search__input");
            mSearchField.RegisterValueChangedCallback(evt =>
            {
                mSearchFilter = evt.newValue?.ToLowerInvariant() ?? "";
                RefreshCategoryDisplay();
            });
            searchBar.Add(mSearchField);

            var clearBtn = new Button(() =>
            {
                mSearchField.value = "";
                mSearchFilter = "";
                RefreshCategoryDisplay();
            });
            clearBtn.AddToClassList("yoki-res-search__clear");
            var clearIcon = new Image { image = KitIcons.GetTexture(KitIcons.DELETE) };
            clearIcon.AddToClassList("yoki-res-search__clear-icon");
            clearBtn.Add(clearIcon);
            searchBar.Add(clearBtn);

            return searchBar;
        }

        /// <summary>
        /// 创建左侧面板（资源列表）
        /// </summary>
        private VisualElement CreateLeftPanel()
        {
            var leftPanel = new VisualElement();
            leftPanel.AddToClassList("left-panel");
            leftPanel.style.flexGrow = 1;

            var listHeader = new VisualElement();
            listHeader.AddToClassList("panel-header");
            var listTitle = new Label("已加载资源（按类型分类）");
            listTitle.AddToClassList("panel-title");
            listHeader.Add(listTitle);
            leftPanel.Add(listHeader);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            leftPanel.Add(scrollView);

            mCategoryContainer = new VisualElement();
            mCategoryContainer.AddToClassList("yoki-res-category-container");
            scrollView.Add(mCategoryContainer);

            return leftPanel;
        }

        /// <summary>
        /// 创建右侧面板（资源详情）
        /// </summary>
        private VisualElement CreateRightPanel()
        {
            var rightPanel = new VisualElement();
            rightPanel.AddToClassList("right-panel");
            rightPanel.style.flexGrow = 1;

            // 详情区域（带滚动）
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            rightPanel.Add(scrollView);

            var detailHeader = new VisualElement();
            detailHeader.AddToClassList("panel-header");
            var detailTitle = new Label("资源详情");
            detailTitle.AddToClassList("panel-title");
            detailHeader.Add(detailTitle);
            scrollView.Add(detailHeader);

            mDetailPanel = new VisualElement();
            mDetailPanel.AddToClassList("yoki-res-detail");
            scrollView.Add(mDetailPanel);

            BuildDetailPanel();

            mHistoryContainer = new VisualElement();
            mHistoryContainer.AddToClassList("yoki-res-history");
            scrollView.Add(mHistoryContainer);

            return rightPanel;
        }

        /// <summary>
        /// 构建详情面板
        /// </summary>
        private void BuildDetailPanel()
        {
            mDetailPanel.Clear();

            var card = new VisualElement();
            card.AddToClassList("yoki-res-detail__card");
            mDetailPanel.Add(card);

            var cardHeader = new VisualElement();
            cardHeader.AddToClassList("yoki-res-detail__card-header");
            var cardTitle = new Label("基本信息");
            cardTitle.AddToClassList("yoki-res-detail__card-title");
            cardHeader.Add(cardTitle);
            card.Add(cardHeader);

            var cardBody = new VisualElement();
            cardBody.AddToClassList("yoki-res-detail__card-body");
            card.Add(cardBody);

            mDetailPath = CreateInfoRow(cardBody, "路径");
            mDetailType = CreateInfoRow(cardBody, "类型");
            mDetailRefCount = CreateInfoRow(cardBody, "引用计数");
            mDetailStatus = CreateInfoRow(cardBody, "状态");
            mDetailSource = CreateInfoRow(cardBody, "来源");
        }

        /// <summary>
        /// 创建信息行
        /// </summary>
        private Label CreateInfoRow(VisualElement parent, string labelText)
        {
            var row = new VisualElement();
            row.AddToClassList("yoki-res-detail__row");

            var label = new Label(labelText);
            label.AddToClassList("yoki-res-detail__label");
            row.Add(label);

            var value = new Label("-");
            value.AddToClassList("yoki-res-detail__value");
            row.Add(value);

            parent.Add(row);
            return value;
        }
    }
}
#endif
