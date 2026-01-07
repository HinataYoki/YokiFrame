#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - 资源监控
    /// </summary>
    public class ResKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "ResKit";
        public override string PageIcon => KitIcons.RESKIT;
        public override int Priority => 35;

        private ListView mAssetListView;
        private Label mLoadedCountLabel;
        private Label mTotalRefCountLabel;
        private VisualElement mDetailPanel;
        private Label mDetailPath;
        private Label mDetailType;
        private Label mDetailRefCount;
        private Label mDetailStatus;
        private Label mDetailSource;
        
        private bool mAutoRefresh = true;
        private float mLastRefreshTime;
        private const float REFRESH_INTERVAL = 0.5f;
        
        private readonly List<ResDebugger.ResInfo> mCachedAssets = new();
        private int mSelectedIndex = -1;

        protected override void BuildUI(VisualElement root)
        {
            // 工具栏
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");
            root.Add(toolbar);

            var refreshBtn = new Button(RefreshData) { text = "刷新" };
            refreshBtn.AddToClassList("toolbar-button");
            toolbar.Add(refreshBtn);

            var autoRefreshToggle = new Toggle { value = mAutoRefresh };
            autoRefreshToggle.RegisterValueChangedCallback(evt => mAutoRefresh = evt.newValue);
            var toggleContainer = new VisualElement();
            toggleContainer.AddToClassList("toolbar-toggle");
            toggleContainer.Add(autoRefreshToggle);
            var toggleLabel = new Label("自动刷新");
            toggleLabel.AddToClassList("toolbar-toggle-label");
            toggleContainer.Add(toggleLabel);
            toolbar.Add(toggleContainer);

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            mLoadedCountLabel = new Label("已加载: 0");
            mLoadedCountLabel.AddToClassList("toolbar-label");
            toolbar.Add(mLoadedCountLabel);

            mTotalRefCountLabel = new Label("总引用: 0");
            mTotalRefCountLabel.AddToClassList("toolbar-label");
            toolbar.Add(mTotalRefCountLabel);

            // 主内容区
            var splitView = new VisualElement();
            splitView.AddToClassList("split-view");
            splitView.style.flexGrow = 1;
            root.Add(splitView);

            // 左侧列表
            var leftPanel = new VisualElement();
            leftPanel.AddToClassList("left-panel");
            splitView.Add(leftPanel);

            var listHeader = new VisualElement();
            listHeader.AddToClassList("panel-header");
            var listTitle = new Label("已加载资源");
            listTitle.AddToClassList("panel-title");
            listHeader.Add(listTitle);
            leftPanel.Add(listHeader);

            mAssetListView = new ListView();
            mAssetListView.makeItem = MakeAssetItem;
            mAssetListView.bindItem = BindAssetItem;
            mAssetListView.itemsSource = mCachedAssets;
            mAssetListView.selectionType = SelectionType.Single;
            mAssetListView.selectionChanged += OnAssetSelected;
            mAssetListView.fixedItemHeight = 48;
            mAssetListView.style.flexGrow = 1;
            leftPanel.Add(mAssetListView);

            // 右侧详情
            var rightPanel = new VisualElement();
            rightPanel.AddToClassList("right-panel");
            splitView.Add(rightPanel);

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
            ShowEmptyState();
        }

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

        private void ShowEmptyState()
        {
            mDetailPath.text = "-";
            mDetailType.text = "-";
            mDetailRefCount.text = "-";
            mDetailStatus.text = "-";
            mDetailSource.text = "-";
        }

        private VisualElement MakeAssetItem()
        {
            var item = new VisualElement();
            item.AddToClassList("list-item");

            var indicator = new VisualElement();
            indicator.AddToClassList("list-item-indicator");
            indicator.name = "indicator";
            item.Add(indicator);

            var infoContainer = new VisualElement();
            infoContainer.style.flexGrow = 1;

            var pathLabel = new Label();
            pathLabel.name = "path";
            pathLabel.AddToClassList("list-item-label");
            infoContainer.Add(pathLabel);

            var typeLabel = new Label();
            typeLabel.name = "type";
            typeLabel.style.fontSize = 10;
            typeLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.55f));
            infoContainer.Add(typeLabel);

            item.Add(infoContainer);

            var refCountBadge = new Label();
            refCountBadge.name = "refCount";
            refCountBadge.AddToClassList("list-item-count");
            item.Add(refCountBadge);

            return item;
        }

        private void BindAssetItem(VisualElement element, int index)
        {
            if (index < 0 || index >= mCachedAssets.Count) return;

            var info = mCachedAssets[index];

            var pathLabel = element.Q<Label>("path");
            pathLabel.text = GetAssetName(info.Path);

            var typeLabel = element.Q<Label>("type");
            // 显示类型和来源
            var sourceTag = info.Source == ResDebugger.ResSource.ResKit ? "[ResKit]" : "[Loader]";
            typeLabel.text = $"{info.TypeName} {sourceTag}";

            var refCountBadge = element.Q<Label>("refCount");
            refCountBadge.text = info.RefCount.ToString();

            var indicator = element.Q("indicator");
            indicator.RemoveFromClassList("active");
            indicator.RemoveFromClassList("inactive");
            indicator.AddToClassList(info.IsDone ? "active" : "inactive");
        }

        private string GetAssetName(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            
            // 只显示文件名
            var lastSlash = path.LastIndexOf('/');
            return lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
        }

        private void OnAssetSelected(IEnumerable<object> selection)
        {
            mSelectedIndex = mAssetListView.selectedIndex;
            
            if (mSelectedIndex < 0 || mSelectedIndex >= mCachedAssets.Count)
            {
                ShowEmptyState();
                return;
            }

            var info = mCachedAssets[mSelectedIndex];
            mDetailPath.text = info.Path;
            mDetailType.text = info.TypeName;
            mDetailRefCount.text = info.RefCount.ToString();
            mDetailStatus.text = info.IsDone ? "已加载" : "加载中";
            mDetailSource.text = info.Source == ResDebugger.ResSource.ResKit 
                ? "ResKit (缓存管理)" 
                : "Loader (直接加载)";

            // 高亮引用计数
            mDetailRefCount.RemoveFromClassList("highlight");
            if (info.RefCount > 1)
            {
                mDetailRefCount.AddToClassList("highlight");
            }
        }

        private void RefreshData()
        {
            mCachedAssets.Clear();
            mCachedAssets.AddRange(ResDebugger.GetLoadedAssets());
            
            mAssetListView.RefreshItems();
            
            mLoadedCountLabel.text = $"已加载: {ResDebugger.GetLoadedCount()}";
            mTotalRefCountLabel.text = $"总引用: {ResDebugger.GetTotalRefCount()}";

            // 保持选中状态
            if (mSelectedIndex >= 0 && mSelectedIndex < mCachedAssets.Count)
            {
                mAssetListView.SetSelection(mSelectedIndex);
            }
        }

        public override void OnUpdate()
        {
            if (!mAutoRefresh) return;
            if (!EditorApplication.isPlaying) return;

            var now = Time.realtimeSinceStartup;
            if (now - mLastRefreshTime < REFRESH_INTERVAL) return;
            
            mLastRefreshTime = now;
            RefreshData();
        }
    }
}
#endif
