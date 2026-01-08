#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// BuffKit 工具页面 - 运行时 Buff 监控（响应式）
    /// </summary>
    public class BuffKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "BuffKit";
        public override string PageIcon => KitIcons.BUFFKIT;
        public override int Priority => 25;

        private const float THROTTLE_INTERVAL = 0.1f;

        // UI 元素引用
        private ListView mContainerListView;
        private VisualElement mDetailPanel;
        private Label mContainerCountLabel;

        // 数据缓存
        private readonly List<BuffContainer> mCachedContainers = new(16);
        private BuffContainer mSelectedContainer;

        // 节流器
        private Throttle mRefreshThrottle;

        protected override void BuildUI(VisualElement root)
        {
            // 工具栏
            var toolbar = CreateToolbar();
            root.Add(toolbar);

            var helpLabel = new Label("运行时 Buff 监控（响应式）");
            helpLabel.AddToClassList("toolbar-label");
            toolbar.Add(helpLabel);

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            mContainerCountLabel = new Label("容器: 0");
            mContainerCountLabel.AddToClassList("toolbar-label");
            toolbar.Add(mContainerCountLabel);

            // 内容区域
            var content = new VisualElement();
            content.AddToClassList("content-area");
            root.Add(content);

            // 分割面板
            var splitView = CreateSplitView(250f);
            content.Add(splitView);

            // 左侧：容器列表
            var leftPanel = new VisualElement();
            leftPanel.AddToClassList("left-panel");
            splitView.Add(leftPanel);

            var leftHeader = CreatePanelHeader("活跃容器");
            leftPanel.Add(leftHeader);

            BuildContainerListView();
            mContainerListView.style.flexGrow = 1;
            leftPanel.Add(mContainerListView);

            // 右侧：详情面板
            mDetailPanel = new VisualElement();
            mDetailPanel.AddToClassList("right-panel");
            splitView.Add(mDetailPanel);

            UpdateDetailPanel();

            // 初始化节流器
            mRefreshThrottle = new Throttle(THROTTLE_INTERVAL);

            // 订阅 Buff 变化事件
            SubscribeBuffEvents();
        }

        private void BuildContainerListView()
        {
            mContainerListView = new ListView();
            mContainerListView.fixedItemHeight = 32;
            mContainerListView.makeItem = () =>
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
            };
            mContainerListView.bindItem = (element, index) =>
            {
                var container = mCachedContainers[index];
                var indicator = element.Q<VisualElement>(className: "list-item-indicator");
                var label = element.Q<Label>(className: "list-item-label");
                var countLabel = element.Q<Label>(className: "list-item-count");

                indicator.RemoveFromClassList("active");
                indicator.RemoveFromClassList("inactive");
                indicator.AddToClassList(container.Count > 0 ? "active" : "inactive");

                label.text = $"Container #{index}";
                countLabel.text = $"[{container.Count}]";
            };
#if UNITY_2022_1_OR_NEWER
            mContainerListView.selectionChanged += OnContainerSelected;
#else
            mContainerListView.onSelectionChange += OnContainerSelected;
#endif
        }

        private void SubscribeBuffEvents()
        {
            // 订阅 Buff 添加事件
            Subscriptions.Add(EditorDataBridge.Subscribe<BuffAddedEvent>(
                DataChannels.BUFF_ADDED,
                _ => RequestRefresh()));

            // 订阅 Buff 移除事件
            Subscriptions.Add(EditorDataBridge.Subscribe<BuffRemovedEvent>(
                DataChannels.BUFF_REMOVED,
                _ => RequestRefresh()));
        }

        private void RequestRefresh()
        {
            if (!IsPlaying) return;
            
            mRefreshThrottle.Execute(RefreshContainerList);
        }

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

        private void UpdateDetailPanel()
        {
            mDetailPanel.Clear();

            if (mSelectedContainer == null)
            {
                var header = CreatePanelHeader("Buff 详情");
                mDetailPanel.Add(header);
                mDetailPanel.Add(CreateHelpBox("选择左侧容器查看详情"));
                return;
            }

            var container = mSelectedContainer;

            var headerWithName = CreatePanelHeader($"容器详情 (Buff 数量: {container.Count})");
            mDetailPanel.Add(headerWithName);

            // 基本信息
            var infoBox = new VisualElement();
            infoBox.AddToClassList("info-box");
            mDetailPanel.Add(infoBox);

            AddInfoRow(infoBox, "Buff 数量:", container.Count.ToString());
            AddInfoRow(infoBox, "已释放:", container.IsDisposed.ToString());

            // Buff 列表
            var buffsHeader = CreatePanelHeader("活跃 Buff");
            mDetailPanel.Add(buffsHeader);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            mDetailPanel.Add(scrollView);

            // 遍历所有 Buff
            var tempList = new List<BuffInstance>();
            for (int buffId = 0; buffId < 10000; buffId++)
            {
                if (container.Has(buffId))
                {
                    container.GetAll(buffId, tempList);
                    foreach (var instance in tempList)
                    {
                        var buffItem = CreateBuffItem(instance);
                        scrollView.Add(buffItem);
                    }
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
                valueElement.AddToClassList("highlight");
            row.Add(valueElement);

            parent.Add(row);
        }

        private VisualElement CreateBuffItem(BuffInstance instance)
        {
            var item = new VisualElement();
            item.AddToClassList("state-item");

            var nameLabel = new Label($"Buff #{instance.BuffId}");
            nameLabel.AddToClassList("state-name");
            item.Add(nameLabel);

            var stackLabel = new Label($"x{instance.StackCount}");
            stackLabel.AddToClassList("state-type");
            stackLabel.style.width = 40;
            item.Add(stackLabel);

            var durationLabel = new Label(instance.IsPermanent ? "永久" : $"{instance.RemainingDuration:F1}s");
            durationLabel.AddToClassList("state-type");
            durationLabel.style.width = 60;
            item.Add(durationLabel);

            return item;
        }

        public override void OnActivate()
        {
            base.OnActivate();
            
            // 进入 PlayMode 时刷新一次
            if (IsPlaying)
            {
                RefreshContainerList();
            }
        }

        private void RefreshContainerList()
        {
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

            mContainerCountLabel.text = $"容器: {mCachedContainers.Count}";
            mContainerListView.itemsSource = mCachedContainers;
            mContainerListView.RefreshItems();

            if (mSelectedContainer != null)
            {
                UpdateDetailPanel();
            }
        }
    }
}
#endif
