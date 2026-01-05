using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 工具页面 - UI Toolkit 版本
    /// </summary>
    public class EventKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "EventKit";
        public override int Priority => 10;

        private const float REFRESH_INTERVAL = 0.5f;

        private enum ViewMode { Runtime, History, CodeScan }
        private enum EventCategory { Enum, Type, String }

        private ViewMode mViewMode = ViewMode.Runtime;
        private EventCategory mSelectedCategory = EventCategory.Enum;
        private string mSelectedEventKey;
        private double mLastRefreshTime;

        // UI 元素引用
        private VisualElement mRuntimeView;
        private VisualElement mHistoryView;
        private VisualElement mCodeScanView;
        private VisualElement mToolbarButtons;
        private ListView mEventListView;
        private VisualElement mListenerPanel;
        private ListView mHistoryListView;
        private Label mHistoryCountLabel;
        private TextField mScanFolderField;
        private ListView mScanResultsListView;

        // 数据缓存
        private readonly List<EventNodeData> mCachedNodes = new();
        private readonly List<ListenerDisplayData> mCachedListeners = new();
        private string mScanFolder = "Assets/Scripts";
        private readonly List<EventCodeScanner.ScanResult> mScanResults = new();

        protected override void BuildUI(VisualElement root)
        {
            // 工具栏
            var toolbar = CreateToolbar();
            root.Add(toolbar);
            
            mToolbarButtons = new VisualElement();
            mToolbarButtons.style.flexDirection = FlexDirection.Row;
            toolbar.Add(mToolbarButtons);
            
            AddViewModeButton("运行时监控", ViewMode.Runtime);
            AddViewModeButton("事件历史", ViewMode.History);
            AddViewModeButton("代码扫描", ViewMode.CodeScan);
            
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });
            
            // 内容区域
            var content = new VisualElement();
            content.AddToClassList("content-area");
            root.Add(content);
            
            // 创建三个视图
            mRuntimeView = CreateRuntimeView();
            mHistoryView = CreateHistoryView();
            mCodeScanView = CreateCodeScanView();
            
            content.Add(mRuntimeView);
            content.Add(mHistoryView);
            content.Add(mCodeScanView);
            
            SwitchView(ViewMode.Runtime);
        }

        private void AddViewModeButton(string text, ViewMode mode)
        {
            var button = CreateToolbarButton(text, () => SwitchView(mode));
            button.name = $"btn_{mode}";
            mToolbarButtons.Add(button);
        }

        private void SwitchView(ViewMode mode)
        {
            mViewMode = mode;
            
            mRuntimeView.style.display = mode == ViewMode.Runtime ? DisplayStyle.Flex : DisplayStyle.None;
            mHistoryView.style.display = mode == ViewMode.History ? DisplayStyle.Flex : DisplayStyle.None;
            mCodeScanView.style.display = mode == ViewMode.CodeScan ? DisplayStyle.Flex : DisplayStyle.None;
            
            // 更新按钮状态
            foreach (var child in mToolbarButtons.Children())
            {
                if (child is Button btn)
                {
                    var isSelected = btn.name == $"btn_{mode}";
                    if (isSelected)
                        btn.AddToClassList("selected");
                    else
                        btn.RemoveFromClassList("selected");
                }
            }
        }

        #region Runtime View

        private VisualElement CreateRuntimeView()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            
            // 分类工具栏
            var categoryBar = CreateToolbar();
            container.Add(categoryBar);
            
            var categoryLabel = new Label("事件类型:");
            categoryLabel.AddToClassList("toolbar-label");
            categoryBar.Add(categoryLabel);
            
            AddCategoryButton(categoryBar, "Enum", EventCategory.Enum);
            AddCategoryButton(categoryBar, "Type", EventCategory.Type);
            AddCategoryButton(categoryBar, "String", EventCategory.String);
            
            categoryBar.Add(new VisualElement { style = { flexGrow = 1 } });
            
            var refreshBtn = CreateToolbarButton("刷新", RefreshEventData);
            categoryBar.Add(refreshBtn);
            
            // 分割面板
            var splitView = CreateSplitView(280f);
            container.Add(splitView);
            
            // 左侧：事件列表
            var leftPanel = new VisualElement();
            leftPanel.AddToClassList("left-panel");
            splitView.Add(leftPanel);
            
            var leftHeader = CreatePanelHeader("已注册事件");
            leftPanel.Add(leftHeader);
            
            mEventListView = new ListView();
            mEventListView.makeItem = () =>
            {
                var item = new VisualElement();
                item.AddToClassList("list-item");
                
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
            mEventListView.bindItem = (element, index) =>
            {
                var node = mCachedNodes[index];
                var indicator = element.Q<VisualElement>(className: "list-item-indicator");
                var label = element.Q<Label>(className: "list-item-label");
                var count = element.Q<Label>(className: "list-item-count");
                
                indicator.RemoveFromClassList("active");
                indicator.RemoveFromClassList("inactive");
                indicator.AddToClassList(node.ListenerCount > 0 ? "active" : "inactive");
                
                label.text = node.DisplayName;
                count.text = $"[{node.ListenerCount}]";
            };
            mEventListView.selectionChanged += OnEventSelected;
            mEventListView.style.flexGrow = 1;
            leftPanel.Add(mEventListView);
            
            // 右侧：监听器详情
            var rightPanel = new VisualElement();
            rightPanel.AddToClassList("right-panel");
            splitView.Add(rightPanel);
            
            mListenerPanel = rightPanel;
            
            return container;
        }

        private void AddCategoryButton(VisualElement parent, string text, EventCategory category)
        {
            var button = CreateToolbarButton(text, () => SwitchCategory(category));
            button.name = $"cat_{category}";
            if (category == mSelectedCategory)
                button.AddToClassList("selected");
            parent.Add(button);
        }

        private void SwitchCategory(EventCategory category)
        {
            mSelectedCategory = category;
            mSelectedEventKey = null;
            
            // 更新按钮状态
            var toolbar = mRuntimeView.Q<VisualElement>(className: "toolbar");
            if (toolbar != null)
            {
                foreach (var child in toolbar.Children())
                {
                    if (child is Button btn && btn.name?.StartsWith("cat_") == true)
                    {
                        var isSelected = btn.name == $"cat_{category}";
                        if (isSelected)
                            btn.AddToClassList("selected");
                        else
                            btn.RemoveFromClassList("selected");
                    }
                }
            }
            
            RefreshEventData();
        }

        private void OnEventSelected(IEnumerable<object> selection)
        {
            foreach (var item in selection)
            {
                if (item is EventNodeData node)
                {
                    mSelectedEventKey = node.Key;
                    RefreshListenerData(node);
                    UpdateListenerPanel();
                    return;
                }
            }
        }

        private void UpdateListenerPanel()
        {
            mListenerPanel.Clear();
            
            if (string.IsNullOrEmpty(mSelectedEventKey))
            {
                var header = CreatePanelHeader("监听器详情");
                mListenerPanel.Add(header);
                mListenerPanel.Add(CreateHelpBox("选择左侧事件查看监听器详情"));
                return;
            }
            
            var headerWithKey = CreatePanelHeader($"监听器详情 - {mSelectedEventKey}");
            mListenerPanel.Add(headerWithKey);
            
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            mListenerPanel.Add(scrollView);
            
            if (mCachedListeners.Count == 0)
            {
                scrollView.Add(CreateEmptyState("暂无监听器"));
            }
            else
            {
                for (int i = 0; i < mCachedListeners.Count; i++)
                {
                    var listener = mCachedListeners[i];
                    var item = CreateListenerItem(i, listener);
                    scrollView.Add(item);
                }
            }
        }

        private VisualElement CreateListenerItem(int index, ListenerDisplayData data)
        {
            var item = new VisualElement();
            item.AddToClassList("info-box");
            
            var titleRow = new VisualElement();
            titleRow.AddToClassList("info-row");
            
            var indexLabel = new Label($"#{index + 1}");
            indexLabel.AddToClassList("info-label");
            titleRow.Add(indexLabel);
            
            var nameLabel = new Label($"{data.TargetType}.{data.MethodName}");
            nameLabel.AddToClassList("info-value");
            nameLabel.AddToClassList("highlight");
            titleRow.Add(nameLabel);
            
            item.Add(titleRow);
            
            if (!string.IsNullOrEmpty(data.FilePath))
            {
                var pathRow = new VisualElement();
                pathRow.style.flexDirection = FlexDirection.Row;
                pathRow.style.alignItems = Align.Center;
                
                var pathLabel = new Label($"注册位置: {data.FilePath}:{data.LineNumber}");
                pathLabel.style.flexGrow = 1;
                pathLabel.style.fontSize = 10;
                pathLabel.style.color = new StyleColor(new UnityEngine.Color(0.6f, 0.6f, 0.6f));
                pathRow.Add(pathLabel);
                
                var jumpBtn = new Button(() => OpenFileAtLine(data.FilePath, data.LineNumber)) { text = "跳转" };
                jumpBtn.style.height = 18;
                jumpBtn.style.fontSize = 10;
                pathRow.Add(jumpBtn);
                
                item.Add(pathRow);
            }
            
            return item;
        }

        #endregion

        #region History View

        private VisualElement CreateHistoryView()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            
            // 工具栏
            var toolbar = CreateToolbar();
            container.Add(toolbar);
            
            var recordToggle = CreateToolbarToggle("记录Send", EasyEventDebugger.RecordSendEvents, 
                v => EasyEventDebugger.RecordSendEvents = v);
            toolbar.Add(recordToggle);
            
            var stackToggle = CreateToolbarToggle("堆栈", EasyEventDebugger.RecordSendStackTrace,
                v => EasyEventDebugger.RecordSendStackTrace = v);
            toolbar.Add(stackToggle);
            
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });
            
            mHistoryCountLabel = new Label("记录: 0/500");
            mHistoryCountLabel.AddToClassList("toolbar-label");
            toolbar.Add(mHistoryCountLabel);
            
            var clearBtn = CreateToolbarButton("清空", () =>
            {
                EasyEventDebugger.ClearHistory();
                RefreshHistoryList();
            });
            toolbar.Add(clearBtn);
            
            // 历史列表
            mHistoryListView = new ListView();
            mHistoryListView.makeItem = CreateHistoryItem;
            mHistoryListView.bindItem = BindHistoryItem;
            mHistoryListView.style.flexGrow = 1;
            container.Add(mHistoryListView);
            
            return container;
        }

        private VisualElement CreateHistoryItem()
        {
            var item = new VisualElement();
            item.AddToClassList("history-item");
            
            var time = new Label();
            time.AddToClassList("history-time");
            item.Add(time);
            
            var actionBadge = new Label();
            actionBadge.AddToClassList("history-badge");
            item.Add(actionBadge);
            
            var typeBadge = new Label();
            typeBadge.AddToClassList("history-badge");
            item.Add(typeBadge);
            
            var key = new Label();
            key.AddToClassList("history-key");
            item.Add(key);
            
            var args = new Label();
            args.AddToClassList("history-args");
            item.Add(args);
            
            return item;
        }

        private void BindHistoryItem(VisualElement element, int index)
        {
            var history = EasyEventDebugger.EventHistory;
            var entry = history[history.Count - 1 - index];
            
            var labels = element.Query<Label>().ToList();
            if (labels.Count < 5) return;
            
            labels[0].text = $"{entry.Time:F2}s";
            
            labels[1].text = entry.Action;
            labels[1].RemoveFromClassList("register");
            labels[1].RemoveFromClassList("unregister");
            labels[1].RemoveFromClassList("send");
            labels[1].AddToClassList(entry.Action.ToLower());
            
            labels[2].text = entry.EventType;
            labels[2].RemoveFromClassList("enum");
            labels[2].RemoveFromClassList("type");
            labels[2].RemoveFromClassList("string");
            labels[2].AddToClassList(entry.EventType.ToLower());
            
            labels[3].text = entry.EventKey;
            labels[4].text = string.IsNullOrEmpty(entry.Args) ? "" : $"({entry.Args})";
        }

        private void RefreshHistoryList()
        {
            var history = EasyEventDebugger.EventHistory;
            mHistoryListView.itemsSource = new int[history.Count];
            mHistoryListView.RefreshItems();
            mHistoryCountLabel.text = $"记录: {history.Count}/{EasyEventDebugger.MAX_HISTORY_COUNT}";
        }

        #endregion

        #region Code Scan View

        private VisualElement CreateCodeScanView()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            
            // 工具栏
            var toolbar = CreateToolbar();
            container.Add(toolbar);
            
            var folderLabel = new Label("扫描目录:");
            folderLabel.AddToClassList("toolbar-label");
            toolbar.Add(folderLabel);
            
            mScanFolderField = new TextField();
            mScanFolderField.value = mScanFolder;
            mScanFolderField.style.width = 200;
            mScanFolderField.RegisterValueChangedCallback(evt => mScanFolder = evt.newValue);
            toolbar.Add(mScanFolderField);
            
            var browseBtn = CreateToolbarButton("...", () =>
            {
                var folder = EditorUtility.OpenFolderPanel("选择扫描目录", mScanFolder, "");
                if (!string.IsNullOrEmpty(folder))
                {
                    var idx = folder.IndexOf("Assets", StringComparison.Ordinal);
                    mScanFolder = idx >= 0 ? folder[idx..] : folder;
                    mScanFolderField.value = mScanFolder;
                }
            });
            toolbar.Add(browseBtn);
            
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });
            
            var scanBtn = CreateToolbarButton("扫描", () =>
            {
                mScanResults.Clear();
                mScanResults.AddRange(EventCodeScanner.ScanFolder(mScanFolder, true));
                RefreshScanResults();
            });
            toolbar.Add(scanBtn);
            
            // 结果列表
            mScanResultsListView = new ListView();
            mScanResultsListView.makeItem = CreateScanResultItem;
            mScanResultsListView.bindItem = BindScanResultItem;
            mScanResultsListView.style.flexGrow = 1;
            container.Add(mScanResultsListView);
            
            return container;
        }

        private VisualElement CreateScanResultItem()
        {
            var item = new VisualElement();
            item.AddToClassList("history-item");
            
            var typeBadge = new Label();
            typeBadge.AddToClassList("history-badge");
            item.Add(typeBadge);
            
            var callBadge = new Label();
            callBadge.AddToClassList("history-badge");
            item.Add(callBadge);
            
            var key = new Label();
            key.AddToClassList("history-key");
            key.style.width = 150;
            item.Add(key);
            
            var path = new Label();
            path.style.flexGrow = 1;
            path.style.fontSize = 10;
            path.style.color = new StyleColor(new UnityEngine.Color(0.6f, 0.6f, 0.6f));
            item.Add(path);
            
            var jumpBtn = new Button { text = "跳转" };
            jumpBtn.style.height = 18;
            jumpBtn.style.fontSize = 10;
            item.Add(jumpBtn);
            
            return item;
        }

        private void BindScanResultItem(VisualElement element, int index)
        {
            var result = mScanResults[index];
            
            var labels = element.Query<Label>().ToList();
            if (labels.Count < 4) return;
            
            labels[0].text = result.EventType;
            labels[0].RemoveFromClassList("enum");
            labels[0].RemoveFromClassList("type");
            labels[0].RemoveFromClassList("string");
            labels[0].AddToClassList(result.EventType.ToLower());
            
            labels[1].text = result.CallType;
            labels[1].RemoveFromClassList("register");
            labels[1].RemoveFromClassList("send");
            labels[1].AddToClassList(result.CallType.ToLower());
            
            labels[2].text = result.EventKey;
            
            var shortPath = result.FilePath.Length > 40 ? "..." + result.FilePath[^37..] : result.FilePath;
            labels[3].text = $"{shortPath}:{result.LineNumber}";
            
            var jumpBtn = element.Q<Button>();
            if (jumpBtn != null)
            {
                jumpBtn.clicked -= null;
                jumpBtn.clicked += () => OpenFileAtLine(result.FilePath, result.LineNumber);
            }
        }

        private void RefreshScanResults()
        {
            mScanResultsListView.itemsSource = mScanResults;
            mScanResultsListView.RefreshItems();
        }

        #endregion

        #region Data Refresh

        public override void OnUpdate()
        {
            if (!IsPlaying) return;
            if (mViewMode != ViewMode.Runtime && mViewMode != ViewMode.History) return;
            
            if (EditorApplication.timeSinceStartup - mLastRefreshTime > REFRESH_INTERVAL)
            {
                if (mViewMode == ViewMode.Runtime)
                    RefreshEventData();
                else if (mViewMode == ViewMode.History)
                    RefreshHistoryList();
                    
                mLastRefreshTime = EditorApplication.timeSinceStartup;
            }
        }

        private void RefreshEventData()
        {
            mCachedNodes.Clear();

            switch (mSelectedCategory)
            {
                case EventCategory.Enum:
                    foreach (var kvp in EventKit.Enum.GetAllEvents())
                    {
                        var enumName = Enum.GetName(kvp.Key.EnumType, kvp.Key.EnumValue) ?? kvp.Key.EnumValue.ToString();
                        mCachedNodes.Add(new EventNodeData
                        {
                            Key = $"Enum_{kvp.Key.EnumType.FullName}_{kvp.Key.EnumValue}",
                            DisplayName = $"{kvp.Key.EnumType.Name}.{enumName}",
                            ListenerCount = GetTotalListenerCount(kvp.Value),
                            EventsRef = kvp.Value
                        });
                    }
                    break;

                case EventCategory.Type:
                    foreach (var kvp in EventKit.Type.GetAllEvents())
                    {
                        mCachedNodes.Add(new EventNodeData
                        {
                            Key = $"Type_{kvp.Key.FullName}",
                            DisplayName = kvp.Key.Name,
                            ListenerCount = kvp.Value.ListenerCount,
                            EasyEventRef = kvp.Value
                        });
                    }
                    break;

                case EventCategory.String:
#pragma warning disable CS0618
                    foreach (var kvp in EventKit.String.GetAllEvents())
#pragma warning restore CS0618
                    {
                        mCachedNodes.Add(new EventNodeData
                        {
                            Key = $"String_{kvp.Key}",
                            DisplayName = kvp.Key,
                            ListenerCount = GetTotalListenerCount(kvp.Value),
                            EventsRef = kvp.Value
                        });
                    }
                    break;
            }

            mEventListView.itemsSource = mCachedNodes;
            mEventListView.RefreshItems();
        }

        private static int GetTotalListenerCount(EasyEvents events)
        {
            var count = 0;
            foreach (var kvp in events.GetAllEvents())
                count += kvp.Value.ListenerCount;
            return count;
        }

        private void RefreshListenerData(EventNodeData node)
        {
            mCachedListeners.Clear();

            IEnumerable<Delegate> listeners = null;

            if (node.EasyEventRef != null)
                listeners = node.EasyEventRef.GetListeners();
            else if (node.EventsRef != null)
            {
                var list = new List<Delegate>();
                foreach (var kvp in node.EventsRef.GetAllEvents())
                    foreach (var del in kvp.Value.GetListeners())
                        list.Add(del);
                listeners = list;
            }

            if (listeners == null) return;

            foreach (var del in listeners)
            {
                var data = new ListenerDisplayData
                {
                    TargetType = del.Target?.GetType().Name ?? del.Method?.DeclaringType?.Name ?? "Unknown",
                    MethodName = del.Method?.Name ?? "Unknown"
                };

                if (EasyEventDebugger.TryGetDebugInfo(del, out var debugInfo))
                {
                    data.FilePath = debugInfo.FilePath;
                    data.LineNumber = debugInfo.LineNumber;
                    data.StackTrace = debugInfo.StackTrace;
                }

                mCachedListeners.Add(data);
            }
        }

        #endregion

        private static void OpenFileAtLine(string filePath, int line)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
            if (asset != null)
                AssetDatabase.OpenAsset(asset, line);
        }

        private struct EventNodeData
        {
            public string Key;
            public string DisplayName;
            public int ListenerCount;
            public EasyEvents EventsRef;
            public IEasyEvent EasyEventRef;
        }

        private struct ListenerDisplayData
        {
            public string TargetType;
            public string MethodName;
            public string FilePath;
            public int LineNumber;
            public string StackTrace;
        }
    }
}
