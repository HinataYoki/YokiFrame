#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - 资源监控（按类型分类 + 搜索）
    /// </summary>
    public class ResKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "ResKit";
        public override string PageIcon => KitIcons.RESKIT;
        public override int Priority => 35;

        // UI 元素
        private TextField mSearchField;
        private VisualElement mCategoryContainer;
        private Label mLoadedCountLabel;
        private Label mTotalRefCountLabel;
        private VisualElement mDetailPanel;
        private Label mDetailPath;
        private Label mDetailType;
        private Label mDetailRefCount;
        private Label mDetailStatus;
        private Label mDetailSource;
        
        // 卸载历史记录 UI
        private VisualElement mHistoryContainer;
        private Label mHistoryCountLabel;
        
        // 状态
        private bool mAutoRefresh = true;
        private float mLastRefreshTime;
        private const float REFRESH_INTERVAL = 0.5f;
        private string mSearchFilter = "";
        
        // 数据
        private readonly List<ResDebugger.ResInfo> mAllAssets = new();
        private readonly Dictionary<string, CategoryPanel> mCategoryPanels = new();
        private ResDebugger.ResInfo? mSelectedAsset;

        // 类型颜色映射
        private static readonly Dictionary<string, Color> sTypeColors = new()
        {
            { "AudioClip", new Color(0.25f, 0.55f, 0.90f) },
            { "Texture2D", new Color(0.90f, 0.55f, 0.25f) },
            { "Sprite", new Color(0.55f, 0.85f, 0.35f) },
            { "Material", new Color(0.85f, 0.35f, 0.55f) },
            { "GameObject", new Color(0.60f, 0.35f, 0.85f) },
            { "TextAsset", new Color(0.35f, 0.75f, 0.75f) },
            { "ScriptableObject", new Color(0.75f, 0.75f, 0.35f) },
            { "Shader", new Color(0.55f, 0.55f, 0.75f) },
            { "Font", new Color(0.75f, 0.55f, 0.55f) },
            { "AnimationClip", new Color(0.45f, 0.65f, 0.85f) },
        };

        private struct CategoryPanel
        {
            public VisualElement Root;
            public VisualElement Header;
            public Label NameLabel;
            public Label CountLabel;
            public VisualElement ItemsContainer;
            public Button ExpandBtn;
            public bool IsExpanded;
        }

        protected override void BuildUI(VisualElement root)
        {
            // 顶部工具栏
            var toolbar = CreateToolbar();
            root.Add(toolbar);
            
            // 搜索栏
            var searchBar = CreateSearchBar();
            root.Add(searchBar);

            // 主内容区
            var splitView = new VisualElement();
            splitView.AddToClassList("split-view");
            splitView.style.flexGrow = 1;
            root.Add(splitView);

            // 左侧分类列表
            var leftPanel = CreateLeftPanel();
            splitView.Add(leftPanel);

            // 右侧详情 + 历史记录
            var rightPanel = CreateRightPanel();
            splitView.Add(rightPanel);

            ShowEmptyState();
            
            // 默认显示历史记录面板
            RefreshHistoryDisplay();
        }
        
        private void ClearHistory()
        {
            ResDebugger.ClearUnloadHistory();
            RefreshHistoryDisplay();
        }
        
        private void RefreshHistoryDisplay()
        {
            if (mHistoryContainer == null) return;
            
            // 清空并重建历史记录列表
            mHistoryContainer.Clear();
            
            // 历史记录头部
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));
            
            var titleLabel = new Label("📜 卸载历史记录");
            titleLabel.style.fontSize = 13;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.92f));
            titleLabel.style.flexGrow = 1;
            header.Add(titleLabel);
            
            var history = ResDebugger.GetUnloadHistory();
            mHistoryCountLabel = new Label($"共 {history.Count} 条");
            mHistoryCountLabel.style.fontSize = 11;
            mHistoryCountLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.65f));
            header.Add(mHistoryCountLabel);
            
            mHistoryContainer.Add(header);
            
            // 历史记录列表
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.maxHeight = 300;
            mHistoryContainer.Add(scrollView);
            
            if (history.Count == 0)
            {
                var emptyLabel = new Label("暂无卸载记录");
                emptyLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.55f));
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.paddingTop = 20;
                emptyLabel.style.paddingBottom = 20;
                scrollView.Add(emptyLabel);
                return;
            }
            
            foreach (var record in history)
            {
                var item = CreateHistoryItem(record);
                scrollView.Add(item);
            }
        }
        
        private VisualElement CreateHistoryItem(ResDebugger.UnloadRecord record)
        {
            var item = new VisualElement();
            item.style.marginLeft = 8;
            item.style.marginRight = 8;
            item.style.marginTop = 4;
            item.style.marginBottom = 4;
            item.style.paddingLeft = 12;
            item.style.paddingRight = 12;
            item.style.paddingTop = 10;
            item.style.paddingBottom = 10;
            item.style.backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.16f));
            item.style.borderTopLeftRadius = item.style.borderTopRightRadius = 6;
            item.style.borderBottomLeftRadius = item.style.borderBottomRightRadius = 6;
            item.style.borderLeftWidth = 3;
            item.style.borderLeftColor = new StyleColor(new Color(0.9f, 0.4f, 0.4f)); // 红色表示卸载
            
            // 第一行：时间 + 类型
            var row1 = new VisualElement();
            row1.style.flexDirection = FlexDirection.Row;
            row1.style.alignItems = Align.Center;
            row1.style.marginBottom = 4;
            
            var timeLabel = new Label(record.UnloadTime.ToString("HH:mm:ss.fff"));
            timeLabel.style.fontSize = 10;
            timeLabel.style.color = new StyleColor(new Color(0.55f, 0.55f, 0.6f));
            timeLabel.style.marginRight = 8;
            row1.Add(timeLabel);
            
            var typeColor = GetTypeColor(record.TypeName);
            var typeLabel = new Label(record.TypeName);
            typeLabel.style.fontSize = 10;
            typeLabel.style.color = new StyleColor(typeColor);
            typeLabel.style.backgroundColor = new StyleColor(new Color(typeColor.r, typeColor.g, typeColor.b, 0.15f));
            typeLabel.style.paddingLeft = 6;
            typeLabel.style.paddingRight = 6;
            typeLabel.style.paddingTop = 2;
            typeLabel.style.paddingBottom = 2;
            typeLabel.style.borderTopLeftRadius = typeLabel.style.borderTopRightRadius = 4;
            typeLabel.style.borderBottomLeftRadius = typeLabel.style.borderBottomRightRadius = 4;
            row1.Add(typeLabel);
            
            item.Add(row1);
            
            // 第二行：路径
            var pathLabel = new Label(GetAssetName(record.Path));
            pathLabel.style.fontSize = 12;
            pathLabel.style.color = new StyleColor(new Color(0.85f, 0.85f, 0.88f));
            pathLabel.style.marginBottom = 4;
            pathLabel.tooltip = record.Path;
            item.Add(pathLabel);
            
            // 第三行：堆栈（可折叠）
            if (!string.IsNullOrEmpty(record.StackTrace) && record.StackTrace != "无可用堆栈信息")
            {
                var stackFoldout = new Foldout { text = "调用堆栈", value = false };
                stackFoldout.style.fontSize = 10;
                stackFoldout.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.65f));
                
                var stackLabel = new Label(record.StackTrace);
                stackLabel.style.fontSize = 10;
                stackLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.55f));
                stackLabel.style.whiteSpace = WhiteSpace.PreWrap;
                stackLabel.style.paddingLeft = 8;
                stackFoldout.Add(stackLabel);
                
                item.Add(stackFoldout);
            }
            
            return item;
        }

        private new VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");

            var refreshBtn = new Button(RefreshData) { text = "🔄 刷新" };
            refreshBtn.AddToClassList("toolbar-button");
            toolbar.Add(refreshBtn);

            var expandAllBtn = new Button(ExpandAllCategories) { text = "⬇ 全部展开" };
            expandAllBtn.AddToClassList("toolbar-button");
            toolbar.Add(expandAllBtn);
            
            var collapseAllBtn = new Button(CollapseAllCategories) { text = "⬆ 全部折叠" };
            collapseAllBtn.AddToClassList("toolbar-button");
            toolbar.Add(collapseAllBtn);
            
            var clearHistoryBtn = new Button(ClearHistory) { text = "🗑 清空历史" };
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

            var searchIcon = new Label("🔍");
            searchIcon.style.marginRight = 8;
            searchIcon.style.fontSize = 12;
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
            }) { text = "✕" };
            clearBtn.style.width = 24;
            clearBtn.style.height = 24;
            searchBar.Add(clearBtn);

            return searchBar;
        }

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
            
            // 卸载历史记录容器（默认显示）
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

        private Color GetTypeColor(string typeName)
        {
            if (sTypeColors.TryGetValue(typeName, out var color))
                return color;
            return new Color(0.50f, 0.50f, 0.55f);
        }

        private string GetTypeIcon(string typeName) => typeName switch
        {
            "AudioClip" => "🔊",
            "Texture2D" => "🖼",
            "Sprite" => "🎨",
            "Material" => "🎭",
            "GameObject" => "📦",
            "TextAsset" => "📄",
            "ScriptableObject" => "⚙",
            "Shader" => "✨",
            "Font" => "🔤",
            "AnimationClip" => "🎬",
            _ => "📁"
        };

        private void CreateOrUpdateCategoryPanel(string typeName, List<ResDebugger.ResInfo> assets)
        {
            if (!mCategoryPanels.TryGetValue(typeName, out var panel))
            {
                panel = CreateCategoryPanel(typeName);
                mCategoryPanels[typeName] = panel;
                mCategoryContainer.Add(panel.Root);
            }

            // 更新计数
            panel.CountLabel.text = $"{assets.Count}";
            
            // 更新内容
            panel.ItemsContainer.Clear();
            foreach (var asset in assets)
            {
                var item = CreateAssetItem(asset);
                panel.ItemsContainer.Add(item);
            }
        }

        private CategoryPanel CreateCategoryPanel(string typeName)
        {
            var accentColor = GetTypeColor(typeName);
            var icon = GetTypeIcon(typeName);

            var root = new VisualElement();
            root.style.marginBottom = 8;
            root.style.backgroundColor = new StyleColor(new Color(0.16f, 0.16f, 0.18f));
            root.style.borderTopLeftRadius = root.style.borderTopRightRadius = 6;
            root.style.borderBottomLeftRadius = root.style.borderBottomRightRadius = 6;
            root.style.borderLeftWidth = 4;
            root.style.borderLeftColor = new StyleColor(accentColor);

            // 头部
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = 40;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            root.Add(header);

            // 展开按钮
            var expandBtn = new Button(() => ToggleCategoryExpand(typeName)) { text = "▶" };
            expandBtn.style.width = 24;
            expandBtn.style.height = 24;
            expandBtn.style.fontSize = 10;
            expandBtn.style.backgroundColor = StyleKeyword.Null;
            expandBtn.style.borderLeftWidth = expandBtn.style.borderRightWidth = 0;
            expandBtn.style.borderTopWidth = expandBtn.style.borderBottomWidth = 0;
            header.Add(expandBtn);

            // 图标和名称
            var nameLabel = new Label($"{icon} {typeName}");
            nameLabel.style.fontSize = 13;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new StyleColor(new Color(0.90f, 0.90f, 0.92f));
            nameLabel.style.marginLeft = 8;
            nameLabel.style.flexGrow = 1;
            header.Add(nameLabel);

            // 计数
            var countLabel = new Label("0");
            countLabel.style.fontSize = 11;
            countLabel.style.color = new StyleColor(accentColor);
            countLabel.style.backgroundColor = new StyleColor(new Color(accentColor.r, accentColor.g, accentColor.b, 0.15f));
            countLabel.style.paddingLeft = 8;
            countLabel.style.paddingRight = 8;
            countLabel.style.paddingTop = 2;
            countLabel.style.paddingBottom = 2;
            countLabel.style.borderTopLeftRadius = countLabel.style.borderTopRightRadius = 10;
            countLabel.style.borderBottomLeftRadius = countLabel.style.borderBottomRightRadius = 10;
            header.Add(countLabel);

            // 内容容器
            var itemsContainer = new VisualElement();
            itemsContainer.style.display = DisplayStyle.None;
            itemsContainer.style.paddingLeft = 36;
            itemsContainer.style.paddingRight = 12;
            itemsContainer.style.paddingBottom = 8;
            itemsContainer.style.borderTopWidth = 1;
            itemsContainer.style.borderTopColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));
            root.Add(itemsContainer);

            return new CategoryPanel
            {
                Root = root,
                Header = header,
                NameLabel = nameLabel,
                CountLabel = countLabel,
                ItemsContainer = itemsContainer,
                ExpandBtn = expandBtn,
                IsExpanded = false
            };
        }

        private VisualElement CreateAssetItem(ResDebugger.ResInfo info)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.height = 32;
            item.style.marginTop = 4;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 8;
            item.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f));
            item.style.borderTopLeftRadius = item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = item.style.borderBottomRightRadius = 4;

            // 状态指示器
            var indicator = new VisualElement();
            indicator.style.width = 6;
            indicator.style.height = 6;
            indicator.style.borderTopLeftRadius = indicator.style.borderTopRightRadius = 3;
            indicator.style.borderBottomLeftRadius = indicator.style.borderBottomRightRadius = 3;
            indicator.style.backgroundColor = new StyleColor(info.IsDone ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.9f, 0.7f, 0.2f));
            indicator.style.marginRight = 8;
            item.Add(indicator);

            // 文件名
            var fileName = GetAssetName(info.Path);
            var nameLabel = new Label(fileName);
            nameLabel.style.flexGrow = 1;
            nameLabel.style.fontSize = 11;
            nameLabel.style.color = new StyleColor(new Color(0.85f, 0.85f, 0.87f));
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(nameLabel);

            // 来源标签
            var sourceTag = info.Source == ResDebugger.ResSource.ResKit ? "ResKit" : "Loader";
            var sourceLabel = new Label(sourceTag);
            sourceLabel.style.fontSize = 9;
            sourceLabel.style.color = new StyleColor(new Color(0.55f, 0.55f, 0.58f));
            sourceLabel.style.marginRight = 8;
            item.Add(sourceLabel);

            // 引用计数
            var refLabel = new Label($"×{info.RefCount}");
            refLabel.style.fontSize = 10;
            refLabel.style.width = 32;
            refLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            refLabel.style.color = new StyleColor(info.RefCount > 1 ? new Color(0.9f, 0.7f, 0.3f) : new Color(0.55f, 0.55f, 0.58f));
            item.Add(refLabel);

            // 点击选中
            item.RegisterCallback<ClickEvent>(_ => SelectAsset(info));

            return item;
        }

        private void ToggleCategoryExpand(string typeName)
        {
            if (!mCategoryPanels.TryGetValue(typeName, out var panel)) return;

            panel.IsExpanded = !panel.IsExpanded;
            panel.ExpandBtn.text = panel.IsExpanded ? "▼" : "▶";
            panel.ItemsContainer.style.display = panel.IsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            mCategoryPanels[typeName] = panel;
        }

        private void ExpandAllCategories()
        {
            foreach (var typeName in mCategoryPanels.Keys.ToList())
            {
                var panel = mCategoryPanels[typeName];
                panel.IsExpanded = true;
                panel.ExpandBtn.text = "▼";
                panel.ItemsContainer.style.display = DisplayStyle.Flex;
                mCategoryPanels[typeName] = panel;
            }
        }

        private void CollapseAllCategories()
        {
            foreach (var typeName in mCategoryPanels.Keys.ToList())
            {
                var panel = mCategoryPanels[typeName];
                panel.IsExpanded = false;
                panel.ExpandBtn.text = "▶";
                panel.ItemsContainer.style.display = DisplayStyle.None;
                mCategoryPanels[typeName] = panel;
            }
        }

        private void SelectAsset(ResDebugger.ResInfo info)
        {
            mSelectedAsset = info;
            mDetailPath.text = info.Path;
            mDetailType.text = info.TypeName;
            mDetailRefCount.text = info.RefCount.ToString();
            mDetailStatus.text = info.IsDone ? "已加载" : "加载中";
            mDetailSource.text = info.Source == ResDebugger.ResSource.ResKit 
                ? "ResKit (缓存管理)" 
                : "Loader (直接加载)";

            mDetailRefCount.RemoveFromClassList("highlight");
            if (info.RefCount > 1)
            {
                mDetailRefCount.AddToClassList("highlight");
            }
        }

        private string GetAssetName(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            var lastSlash = path.LastIndexOf('/');
            return lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
        }

        private void RefreshData()
        {
            mAllAssets.Clear();
            mAllAssets.AddRange(ResDebugger.GetLoadedAssets());
            
            mLoadedCountLabel.text = $"已加载: {ResDebugger.GetLoadedCount()}";
            mTotalRefCountLabel.text = $"总引用: {ResDebugger.GetTotalRefCount()}";

            RefreshCategoryDisplay();
        }

        private void RefreshCategoryDisplay()
        {
            // 按类型分组
            var groupedAssets = new Dictionary<string, List<ResDebugger.ResInfo>>();
            
            foreach (var asset in mAllAssets)
            {
                // 搜索过滤
                if (!string.IsNullOrEmpty(mSearchFilter))
                {
                    var matchPath = asset.Path?.ToLowerInvariant().Contains(mSearchFilter) ?? false;
                    var matchType = asset.TypeName?.ToLowerInvariant().Contains(mSearchFilter) ?? false;
                    if (!matchPath && !matchType) continue;
                }

                var typeName = asset.TypeName ?? "Unknown";
                if (!groupedAssets.TryGetValue(typeName, out var list))
                {
                    list = new List<ResDebugger.ResInfo>();
                    groupedAssets[typeName] = list;
                }
                list.Add(asset);
            }

            // 隐藏空分类
            foreach (var kvp in mCategoryPanels)
            {
                kvp.Value.Root.style.display = groupedAssets.ContainsKey(kvp.Key) 
                    ? DisplayStyle.Flex 
                    : DisplayStyle.None;
            }

            // 更新或创建分类面板
            foreach (var kvp in groupedAssets.OrderBy(x => x.Key))
            {
                CreateOrUpdateCategoryPanel(kvp.Key, kvp.Value);
            }
        }

        public override void OnUpdate()
        {
            if (!mAutoRefresh) return;
            if (!EditorApplication.isPlaying) return;

            var now = Time.realtimeSinceStartup;
            if (now - mLastRefreshTime < REFRESH_INTERVAL) return;
            
            mLastRefreshTime = now;
            
            // 检测卸载的资源
            ResDebugger.DetectUnloadedAssets();
            
            RefreshData();
            RefreshHistoryDisplay();
        }
    }
}
#endif
