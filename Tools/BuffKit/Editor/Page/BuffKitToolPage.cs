#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// BuffKit 工具页。
    /// 用于在编辑器中观察运行时 Buff 容器和活跃 Buff 实例。
    /// </summary>
    [YokiToolPage(
        kit: "BuffKit",
        name: "BuffKit",
        icon: KitIcons.BUFFKIT,
        priority: 25,
        category: YokiPageCategory.Tool)]
    public class BuffKitToolPage : YokiToolPageBase
    {
        private const float THROTTLE_INTERVAL = 0.1f;

        private ListView mContainerListView;
        private VisualElement mDetailPanel;
        private Label mContainerCountLabel;
        private Label mContainerMetricLabel;
        private Label mSelectedMetricLabel;

        private readonly List<BuffContainer> mCachedContainers = new(16);
        private BuffContainer mSelectedContainer;

        private Throttle mRefreshThrottle;

        /// <summary>
        /// 构建 BuffKit 统一工作台入口。
        /// </summary>
        protected override void BuildUI(VisualElement root)
        {
            var scaffold = CreateKitPageScaffold(
                "BuffKit",
                "用于观察运行时 Buff 容器、活跃 Buff 条目与容器状态，保持原有监控功能不变。",
                KitIcons.BUFFKIT,
                "Buff 工作台");
            root.Add(scaffold.Root);
            scaffold.Toolbar.style.display = DisplayStyle.None;

            SetStatusContent(scaffold.StatusBar, CreateKitStatusBanner(
                "运行时监控",
                IsPlaying
                    ? "Buff 监控已连接运行时数据，容器变化会通过编辑器桥接事件自动刷新。"
                    : "Buff 监控建议在 PlayMode 下查看，当前可先预览工具布局。"));

            var metricStrip = CreateKitMetricStrip();
            scaffold.Content.Add(metricStrip);

            var (containerCard, containerValue) = CreateKitMetricCard("容器总数", "0", "当前运行中的 Buff 容器数量", YokiFrameUIComponents.Colors.WorkbenchPrimary);
            mContainerMetricLabel = containerValue;
            metricStrip.Add(containerCard);

            var (selectedCard, selectedValue) = CreateKitMetricCard("当前选中", "-", "未选中容器时显示为空", YokiFrameUIComponents.Colors.WorkbenchPrimary);
            mSelectedMetricLabel = selectedValue;
            metricStrip.Add(selectedCard);

            var splitView = CreateSplitView(260f, "YokiFrame.BuffKit.MainSplitWidth");
            scaffold.Content.Add(splitView);

            splitView.Add(BuildLeftPanel());
            splitView.Add(BuildRightPanel());

            mRefreshThrottle = new Throttle(THROTTLE_INTERVAL);
            SubscribeBuffEvents();
            UpdateDetailPanel();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = CreateToolbar();
            toolbar.AddToClassList("yoki-buff-toolbar");

            var title = new Label("运行时 Buff 监控");
            title.AddToClassList("toolbar-label");
            toolbar.Add(title);

            toolbar.Add(CreateToolbarSpacer());

            mContainerCountLabel = new Label("容器: 0");
            mContainerCountLabel.AddToClassList("toolbar-label");
            toolbar.Add(mContainerCountLabel);

            return toolbar;
        }

        private VisualElement BuildLeftPanel()
        {
            var (panel, body) = CreateKitSectionPanel(
                "活跃容器",
                "显示当前参与运行的 Buff 容器，并按容器维度查看 Buff 数量。",
                KitIcons.BUFFKIT);
            panel.AddToClassList("left-panel");
            panel.AddToClassList("yoki-kit-panel--blue");

            BuildContainerListView();
            mContainerListView.style.flexGrow = 1;
            body.Add(mContainerListView);

            return panel;
        }

        private VisualElement BuildRightPanel()
        {
            mDetailPanel = new VisualElement();
            mDetailPanel.AddToClassList("right-panel");
            mDetailPanel.AddToClassList("yoki-monitor-dashboard");
            mDetailPanel.style.flexDirection = FlexDirection.Column;
            return mDetailPanel;
        }

        /// <summary>
        /// 构建左侧容器列表。
        /// </summary>
        private void BuildContainerListView()
        {
            mContainerListView = new ListView
            {
                fixedItemHeight = 32,
                makeItem = () =>
                {
                    var item = new VisualElement();
                    item.AddToClassList("list-item");
                    item.style.height = 32;
                    item.style.paddingTop = 4;
                    item.style.paddingBottom = 4;

                    var indicator = new VisualElement();
                    indicator.AddToClassList("list-item-indicator");
                    item.Add(indicator);

                    var label = new Label();
                    label.AddToClassList("list-item-label");
                    item.Add(label);

                    var count = new Label();
                    count.AddToClassList("list-item-count");
                    item.Add(count);

                    return item;
                },
                bindItem = (element, index) =>
                {
                    if (index < 0 || index >= mCachedContainers.Count)
                    {
                        return;
                    }

                    var container = mCachedContainers[index];
                    var indicator = element.Q<VisualElement>(className: "list-item-indicator");
                    var label = element.Q<Label>(className: "list-item-label");
                    var countLabel = element.Q<Label>(className: "list-item-count");

                    indicator.RemoveFromClassList("active");
                    indicator.RemoveFromClassList("inactive");
                    indicator.AddToClassList(container.Count > 0 ? "active" : "inactive");

                    label.text = $"Container #{index}";
                    countLabel.text = $"[{container.Count}]";
                }
            };

#if UNITY_2022_1_OR_NEWER
            mContainerListView.selectionChanged += OnContainerSelected;
#else
            mContainerListView.onSelectionChange += OnContainerSelected;
#endif
        }

        /// <summary>
        /// 注册 Buff 监控通道订阅。
        /// </summary>
        private void SubscribeBuffEvents()
        {
            SubscribeChannel<BuffAddedEvent>(DataChannels.BUFF_ADDED, _ => RequestRefresh());
            SubscribeChannel<BuffRemovedEvent>(DataChannels.BUFF_REMOVED, _ => RequestRefresh());
        }

        /// <summary>
        /// 请求刷新 Buff 容器快照。
        /// </summary>
        private void RequestRefresh()
        {
            if (!IsPlaying)
            {
                return;
            }

            mRefreshThrottle.Execute(RefreshContainerList);
        }

        /// <summary>
        /// 处理容器选中变化。
        /// </summary>
        private void OnContainerSelected(IEnumerable<object> selection)
        {
            foreach (var item in selection)
            {
                if (item is BuffContainer container)
                {
                    mSelectedContainer = container;
                    UpdateDetailPanel();
                    return;
                }
            }
        }

        /// <summary>
        /// 根据当前选中容器重建详情面板。
        /// </summary>
        private void UpdateDetailPanel()
        {
            mDetailPanel.Clear();

            if (mSelectedContainer == null)
            {
                mSelectedMetricLabel.text = "-";

                var (emptySection, emptyBody) = CreateKitSectionPanel(
                    "Buff 详情",
                    "先在左侧选择一个容器，再查看基础状态与活跃 Buff 列表。",
                    KitIcons.DOCUMENTATION);
                emptySection.AddToClassList("yoki-kit-panel--blue");
                emptySection.style.flexGrow = 1;
                emptyBody.Add(CreateEmptyState(KitIcons.BUFFKIT, "未选中 Buff 容器", "左侧列表会展示当前运行中的容器快照。"));
                mDetailPanel.Add(emptySection);
                return;
            }

            var container = mSelectedContainer;
            mSelectedMetricLabel.text = $"Buff {container.Count}";

            var (overviewSection, overviewBody) = CreateKitSectionPanel(
                "容器详情",
                $"当前容器包含 {container.Count} 个 Buff，下面列出基础状态与活跃实例。",
                KitIcons.INFO);
            overviewSection.AddToClassList("yoki-kit-panel--blue");
            overviewSection.style.marginBottom = 10;
            mDetailPanel.Add(overviewSection);

            var infoBox = new VisualElement();
            infoBox.AddToClassList("info-box");
            overviewBody.Add(infoBox);

            AddInfoRow(infoBox, "Buff 数量:", container.Count.ToString());
            AddInfoRow(infoBox, "已释放:", container.IsDisposed.ToString());

            var (buffsSection, buffsBody) = CreateKitSectionPanel(
                "活跃 Buff",
                "按实例列出当前容器中的 Buff、层数与剩余持续时间。",
                KitIcons.CHART);
            buffsSection.AddToClassList("yoki-kit-panel--cyan");
            buffsSection.style.flexGrow = 1;
            mDetailPanel.Add(buffsSection);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            buffsBody.Add(scrollView);

            var tempList = new List<BuffInstance>();
            for (int buffId = 0; buffId < 10000; buffId++)
            {
                if (!container.Has(buffId))
                {
                    continue;
                }

                container.GetAll(buffId, tempList);
                foreach (var instance in tempList)
                {
                    scrollView.Add(CreateBuffItem(instance));
                }
            }

            if (scrollView.childCount == 0)
            {
                scrollView.Add(CreateEmptyState("暂无活跃 Buff"));
            }
        }

        private void AddInfoRow(VisualElement parent, string label, string value, bool highlight = false)
        {
            var row = new VisualElement();
            row.AddToClassList("info-row");

            var labelElement = new Label(label);
            labelElement.AddToClassList("info-label");
            row.Add(labelElement);

            var valueElement = new Label(value);
            valueElement.AddToClassList("info-value");
            if (highlight)
            {
                valueElement.AddToClassList("highlight");
            }

            row.Add(valueElement);
            parent.Add(row);
        }

        private VisualElement CreateBuffItem(BuffInstance instance)
        {
            var item = new VisualElement();
            item.AddToClassList("yoki-buff-card");
            item.AddToClassList("yoki-buff-card--positive");

            var header = new VisualElement();
            header.AddToClassList("yoki-buff-card__header");
            item.Add(header);

            var nameLabel = new Label($"Buff #{instance.BuffId}");
            nameLabel.AddToClassList("yoki-buff-card__title");
            header.Add(nameLabel);

            var stackLabel = new Label($"x{instance.StackCount}");
            stackLabel.AddToClassList("state-type");
            stackLabel.style.width = 40;
            header.Add(stackLabel);

            var durationLabel = new Label(instance.IsPermanent ? "永久" : $"{instance.RemainingDuration:F1}s");
            durationLabel.AddToClassList("state-type");
            durationLabel.style.width = 60;
            header.Add(durationLabel);

            return item;
        }

        public override void OnActivate()
        {
            base.OnActivate();
            RefreshContainerList();
        }

        private void RefreshContainerList()
        {
            if (!IsPlaying)
            {
                mCachedContainers.Clear();
                mSelectedContainer = null;
                if (mContainerCountLabel != null) mContainerCountLabel.text = "容器: 0";
                mContainerMetricLabel.text = "0";
                mContainerListView.itemsSource = mCachedContainers;
                mContainerListView.RefreshItems();
                UpdateDetailPanel();
                return;
            }

            mCachedContainers.Clear();

            var activeContainers = BuffKit.ActiveContainers;
            if (activeContainers != null)
            {
                foreach (var container in activeContainers)
                {
                    if (container != null && !container.IsDisposed)
                    {
                        mCachedContainers.Add(container);
                    }
                }
            }

            if (mContainerCountLabel != null) mContainerCountLabel.text = $"容器: {mCachedContainers.Count}";
            mContainerMetricLabel.text = mCachedContainers.Count.ToString();
            mContainerListView.itemsSource = mCachedContainers;
            mContainerListView.RefreshItems();

            if (mSelectedContainer != null && !mCachedContainers.Contains(mSelectedContainer))
            {
                mSelectedContainer = null;
            }

            UpdateDetailPanel();
        }
    }
}
#endif
