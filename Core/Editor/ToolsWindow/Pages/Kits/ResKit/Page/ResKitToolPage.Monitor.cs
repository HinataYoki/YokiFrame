#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 资源监控工作区布局。
    /// </summary>
    public partial class ResKitToolPage
    {
        private void BuildResourceMonitorUI(VisualElement root)
        {
            var metricStrip = CreateKitMetricStrip();
            root.Add(metricStrip);

            var (loadedCard, loadedValue) = CreateKitMetricCard("已加载资源", "0", "当前运行时已加载的资源数量", YokiFrameUIComponents.Colors.WorkbenchPrimary);
            mLoadedCountLabel = loadedValue;
            metricStrip.Add(loadedCard);

            var (refCard, refValue) = CreateKitMetricCard("总引用数", "0", "所有已加载资源的引用计数总和", YokiFrameUIComponents.Colors.WorkbenchPrimary);
            mTotalRefCountLabel = refValue;
            metricStrip.Add(refCard);

            var toolbar = CreateMonitorToolbar();
            root.Add(toolbar);

            var searchBar = CreateSearchBar();
            root.Add(searchBar);

            var content = CreateContentArea();
            root.Add(content);

            var splitView = CreateSplitView(300f, "YokiFrame.ResKit.MainSplitWidth");
            content.Add(splitView);

            splitView.Add(CreateLeftPanel());
            splitView.Add(CreateRightPanel());

            ShowEmptyState();
            RefreshHistoryDisplay();
        }

        private VisualElement CreateMonitorToolbar()
        {
            var toolbar = CreateToolbar();

            toolbar.Add(CreateToolbarButtonWithIcon(KitIcons.REFRESH, "刷新", RefreshData));
            toolbar.Add(CreateToolbarButtonWithIcon(KitIcons.EXPAND, "全部展开", ExpandAllCategories));
            toolbar.Add(CreateToolbarButton("全部折叠", CollapseAllCategories));
            toolbar.Add(CreateToolbarButtonWithIcon(KitIcons.DELETE, "清空历史", ClearHistory));
            toolbar.Add(CreateModernToggle("自动刷新", mAutoRefresh, value => mAutoRefresh = value));
            toolbar.Add(CreateToolbarSpacer());

            return toolbar;
        }

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
                mSearchFilter = evt.newValue?.ToLowerInvariant() ?? string.Empty;
                RefreshCategoryDisplay();
            });
            searchBar.Add(mSearchField);

            var clearBtn = new Button(() =>
            {
                mSearchField.value = string.Empty;
                mSearchFilter = string.Empty;
                RefreshCategoryDisplay();
            });
            clearBtn.AddToClassList("yoki-res-search__clear");

            var clearIcon = new Image { image = KitIcons.GetTexture(KitIcons.DELETE) };
            clearIcon.AddToClassList("yoki-res-search__clear-icon");
            clearBtn.Add(clearIcon);
            searchBar.Add(clearBtn);

            return searchBar;
        }

        private VisualElement CreateLeftPanel()
        {
            var (panel, body) = CreateKitSectionPanel(
                "已加载资源",
                "按资源类型分组展示当前已加载资源，并支持展开、折叠与快速筛选。",
                KitIcons.RESKIT);
            panel.AddToClassList("left-panel");
            panel.AddToClassList("yoki-kit-panel--blue");

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            body.Add(scrollView);

            mCategoryContainer = new VisualElement();
            mCategoryContainer.AddToClassList("yoki-res-category-container");
            scrollView.Add(mCategoryContainer);

            return panel;
        }

        private VisualElement CreateRightPanel()
        {
            var rightPanel = new VisualElement();
            rightPanel.AddToClassList("right-panel");
            rightPanel.AddToClassList("yoki-monitor-dashboard");
            rightPanel.style.flexDirection = FlexDirection.Column;

            var (detailSection, detailBody) = CreateKitSectionPanel(
                "资源详情",
                "查看选中资源的路径、类型、引用计数、加载状态与来源。",
                KitIcons.DOCUMENTATION);
            detailSection.AddToClassList("yoki-kit-panel--blue");
            detailSection.style.marginBottom = 10;
            rightPanel.Add(detailSection);

            mDetailPanel = new VisualElement();
            mDetailPanel.AddToClassList("yoki-res-detail");
            mDetailPanel.style.flexGrow = 0;
            detailBody.Add(mDetailPanel);
            BuildDetailPanel();

            var (historySection, historyBody) = CreateKitSectionPanel(
                "卸载历史",
                "记录最近的资源卸载行为，帮助排查资源释放时机。",
                KitIcons.CHART);
            historySection.AddToClassList("yoki-kit-panel--cyan");
            historySection.style.flexGrow = 1;
            rightPanel.Add(historySection);

            mHistoryContainer = new VisualElement();
            mHistoryContainer.AddToClassList("yoki-res-history");
            mHistoryContainer.style.flexGrow = 1;
            historyBody.Add(mHistoryContainer);

            return rightPanel;
        }

        private void BuildDetailPanel()
        {
            mDetailPanel.Clear();

            var card = new VisualElement();
            card.AddToClassList("yoki-res-detail__card");
            mDetailPanel.Add(card);

            var cardHeader = new VisualElement();
            cardHeader.AddToClassList("yoki-res-detail__card-header");
            cardHeader.Add(CreateSectionHeader("基础信息", KitIcons.INFO));
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
