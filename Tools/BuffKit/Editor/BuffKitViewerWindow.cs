#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// BuffKit 独立查看器窗口 - 运行时 Buff 调试工具
    /// </summary>
    public class BuffKitViewerWindow : EditorWindow
    {
        private const float REFRESH_INTERVAL = 0.2f;

        private double mLastRefreshTime;

        // UI 元素引用
        private ListView mContainerListView;
        private ScrollView mBuffScrollView;
        private Label mContainerCountLabel;
        private Label mSelectedContainerLabel;

        // 数据缓存
        private readonly List<BuffContainer> mCachedContainers = new(16);
        private BuffContainer mSelectedContainer;

        public static void ShowWindow()
        {
            var window = GetWindow<BuffKitViewerWindow>();
            window.titleContent = new GUIContent("BuffKit Viewer");
            window.minSize = new Vector2(600, 400);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            // 工具栏
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 24;
            toolbar.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.alignItems = Align.Center;
            root.Add(toolbar);

            var titleLabel = new Label("BuffKit 运行时监控");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolbar.Add(titleLabel);

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            mContainerCountLabel = new Label("容器: 0");
            toolbar.Add(mContainerCountLabel);

            // 主内容区域
            var content = new VisualElement();
            content.style.flexDirection = FlexDirection.Row;
            content.style.flexGrow = 1;
            root.Add(content);

            // 左侧：容器列表
            var leftPanel = new VisualElement();
            leftPanel.style.width = 200;
            leftPanel.style.borderRightWidth = 1;
            leftPanel.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            content.Add(leftPanel);

            var leftHeader = new Label("活跃容器");
            leftHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            leftHeader.style.paddingLeft = 8;
            leftHeader.style.paddingTop = 4;
            leftHeader.style.paddingBottom = 4;
            leftHeader.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            leftPanel.Add(leftHeader);

            mContainerListView = new ListView();
            mContainerListView.fixedItemHeight = 28;
            mContainerListView.makeItem = MakeContainerItem;
            mContainerListView.bindItem = BindContainerItem;
#if UNITY_2022_1_OR_NEWER
            mContainerListView.selectionChanged += OnContainerSelected;
#else
            mContainerListView.onSelectionChange += OnContainerSelected;
#endif
            mContainerListView.style.flexGrow = 1;
            leftPanel.Add(mContainerListView);

            // 右侧：Buff 详情
            var rightPanel = new VisualElement();
            rightPanel.style.flexGrow = 1;
            content.Add(rightPanel);

            mSelectedContainerLabel = new Label("选择左侧容器查看详情");
            mSelectedContainerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            mSelectedContainerLabel.style.paddingLeft = 8;
            mSelectedContainerLabel.style.paddingTop = 4;
            mSelectedContainerLabel.style.paddingBottom = 4;
            mSelectedContainerLabel.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            rightPanel.Add(mSelectedContainerLabel);

            mBuffScrollView = new ScrollView();
            mBuffScrollView.style.flexGrow = 1;
            rightPanel.Add(mBuffScrollView);

            // 底部状态栏
            var statusBar = new VisualElement();
            statusBar.style.height = 20;
            statusBar.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            statusBar.style.paddingLeft = 8;
            statusBar.style.alignItems = Align.Center;
            statusBar.style.flexDirection = FlexDirection.Row;
            root.Add(statusBar);

            var statusLabel = new Label("运行游戏后自动刷新");
            statusLabel.style.fontSize = 10;
            statusLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            statusBar.Add(statusLabel);
        }

        private VisualElement MakeContainerItem()
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.height = 28;
            item.style.paddingLeft = 8;

            var indicator = new VisualElement();
            indicator.name = "indicator";
            indicator.style.width = 8;
            indicator.style.height = 8;
            indicator.style.borderTopLeftRadius = 4;
            indicator.style.borderTopRightRadius = 4;
            indicator.style.borderBottomLeftRadius = 4;
            indicator.style.borderBottomRightRadius = 4;
            indicator.style.marginRight = 8;
            item.Add(indicator);

            var label = new Label();
            label.name = "label";
            label.style.flexGrow = 1;
            item.Add(label);

            var count = new Label();
            count.name = "count";
            count.style.marginRight = 8;
            count.style.color = new Color(0.6f, 0.6f, 0.6f);
            item.Add(count);

            return item;
        }

        private void BindContainerItem(VisualElement element, int index)
        {
            var container = mCachedContainers[index];
            var indicator = element.Q<VisualElement>("indicator");
            var label = element.Q<Label>("label");
            var countLabel = element.Q<Label>("count");

            indicator.style.backgroundColor = container.Count > 0 
                ? new Color(0.3f, 0.8f, 0.3f) 
                : new Color(0.5f, 0.5f, 0.5f);

            label.text = $"Container #{index}";
            countLabel.text = $"[{container.Count}]";
        }

        private void OnContainerSelected(IEnumerable<object> selection)
        {
            foreach (var item in selection)
            {
                if (item is BuffContainer container)
                {
                    mSelectedContainer = container;
                    UpdateBuffList();
                    return;
                }
            }
        }

        private void UpdateBuffList()
        {
            mBuffScrollView.Clear();

            if (mSelectedContainer == null)
            {
                mSelectedContainerLabel.text = "选择左侧容器查看详情";
                return;
            }

            mSelectedContainerLabel.text = $"容器详情 (Buff 数量: {mSelectedContainer.Count})";

            if (mSelectedContainer.Count == 0)
            {
                var emptyLabel = new Label("暂无活跃 Buff");
                emptyLabel.style.paddingLeft = 16;
                emptyLabel.style.paddingTop = 16;
                emptyLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                mBuffScrollView.Add(emptyLabel);
                return;
            }

            // 遍历所有 Buff
            var tempList = new List<BuffInstance>();
            for (int buffId = 0; buffId < 10000; buffId++)
            {
                if (mSelectedContainer.Has(buffId))
                {
                    mSelectedContainer.GetAll(buffId, tempList);
                    foreach (var instance in tempList)
                    {
                        var buffItem = CreateBuffItem(instance);
                        mBuffScrollView.Add(buffItem);
                    }
                }
            }
        }

        private VisualElement CreateBuffItem(BuffInstance instance)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.height = 32;
            item.style.paddingLeft = 16;
            item.style.paddingRight = 16;
            item.style.borderBottomWidth = 1;
            item.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);

            var idLabel = new Label($"Buff #{instance.BuffId}");
            idLabel.style.width = 100;
            idLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            item.Add(idLabel);

            var stackLabel = new Label($"x{instance.StackCount}");
            stackLabel.style.width = 50;
            stackLabel.style.color = new Color(0.8f, 0.8f, 0.3f);
            item.Add(stackLabel);

            var durationLabel = new Label(instance.IsPermanent ? "永久" : $"{instance.RemainingDuration:F1}s");
            durationLabel.style.width = 80;
            durationLabel.style.color = instance.IsPermanent 
                ? new Color(0.3f, 0.8f, 0.3f) 
                : new Color(0.8f, 0.8f, 0.8f);
            item.Add(durationLabel);

            return item;
        }

        private void Update()
        {
            if (!EditorApplication.isPlaying) return;

            if (EditorApplication.timeSinceStartup - mLastRefreshTime > REFRESH_INTERVAL)
            {
                RefreshContainerList();
                mLastRefreshTime = EditorApplication.timeSinceStartup;
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
                UpdateBuffList();
            }
        }
    }
}
#endif
