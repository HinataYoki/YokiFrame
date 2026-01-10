#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - 资源监控 UI 构建
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

            var splitView = new VisualElement();
            splitView.AddToClassList("split-view");
            splitView.style.flexGrow = 1;
            root.Add(splitView);

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

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

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
            searchBar.style.flexDirection = FlexDirection.Row;
            searchBar.style.alignItems = Align.Center;
            searchBar.style.paddingLeft = 12;
            searchBar.style.paddingRight = 12;
            searchBar.style.paddingTop = 8;
            searchBar.style.paddingBottom = 8;
            searchBar.style.backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.16f));
            searchBar.style.borderBottomWidth = 1;
            searchBar.style.borderBottomColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));

            var searchIcon = new Image { image = KitIcons.GetTexture(KitIcons.TARGET) };
            searchIcon.style.width = 12;
            searchIcon.style.height = 12;
            searchIcon.style.marginRight = 8;
            searchBar.Add(searchIcon);

            mSearchField = new TextField();
            mSearchField.style.flexGrow = 1;
            mSearchField.style.marginRight = 8;
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
            clearBtn.style.width = 24;
            clearBtn.style.height = 24;
            clearBtn.style.paddingLeft = 4;
            clearBtn.style.paddingRight = 4;
            var clearIcon = new Image { image = KitIcons.GetTexture(KitIcons.DELETE) };
            clearIcon.style.width = 14;
            clearIcon.style.height = 14;
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
            mCategoryContainer.style.paddingLeft = 8;
            mCategoryContainer.style.paddingRight = 8;
            mCategoryContainer.style.paddingTop = 8;
            mCategoryContainer.style.paddingBottom = 8;
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

            var detailHeader = new VisualElement();
            detailHeader.AddToClassList("panel-header");
            var detailTitle = new Label("资源详情");
            detailTitle.AddToClassList("panel-title");
            detailHeader.Add(detailTitle);
            rightPanel.Add(detailHeader);

            mDetailPanel = new VisualElement();
            mDetailPanel.style.paddingLeft = 16;
            mDetailPanel.style.paddingRight = 16;
            mDetailPanel.style.paddingTop = 16;
            rightPanel.Add(mDetailPanel);

            BuildDetailPanel();
            
            mHistoryContainer = new VisualElement();
            mHistoryContainer.style.marginTop = 16;
            mHistoryContainer.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f));
            mHistoryContainer.style.borderTopLeftRadius = mHistoryContainer.style.borderTopRightRadius = 8;
            mHistoryContainer.style.borderBottomLeftRadius = mHistoryContainer.style.borderBottomRightRadius = 8;
            mHistoryContainer.style.marginLeft = 16;
            mHistoryContainer.style.marginRight = 16;
            rightPanel.Add(mHistoryContainer);
            
            return rightPanel;
        }

        /// <summary>
        /// 构建详情面板
        /// </summary>
        private void BuildDetailPanel()
        {
            mDetailPanel.Clear();

            var card = new VisualElement();
            card.AddToClassList("card");
            mDetailPanel.Add(card);

            var cardHeader = new VisualElement();
            cardHeader.AddToClassList("card-header");
            var cardTitle = new Label("基本信息");
            cardTitle.AddToClassList("card-title");
            cardHeader.Add(cardTitle);
            card.Add(cardHeader);

            var cardBody = new VisualElement();
            cardBody.AddToClassList("card-body");
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
            row.AddToClassList("info-row");

            var label = new Label(labelText);
            label.AddToClassList("info-label");
            row.Add(label);

            var value = new Label("-");
            value.AddToClassList("info-value");
            row.Add(value);

            parent.Add(row);
            return value;
        }
    }
}
#endif
