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

        private ViewMode mViewMode = ViewMode.Runtime;
        private double mLastRefreshTime;
        
        // 运行时树状图折叠状态缓存
        private readonly HashSet<string> mExpandedFoldouts = new();

        // UI 元素引用
        private VisualElement mRuntimeView;
        private VisualElement mHistoryView;
        private VisualElement mCodeScanView;
        private VisualElement mToolbarButtons;
        private ScrollView mRuntimeScrollView;
        private Label mRuntimeSummaryLabel;
        private ListView mHistoryListView;
        private Label mHistoryCountLabel;
        private TextField mScanFolderField;
        private ScrollView mScanResultsScrollView;
        private Label mScanSummaryLabel;
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
            
            // 工具栏
            var toolbar = CreateToolbar();
            container.Add(toolbar);
            
            var helpLabel = new Label("运行时事件监听器树状图（需要运行游戏）");
            helpLabel.AddToClassList("toolbar-label");
            toolbar.Add(helpLabel);
            
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });
            
            mRuntimeSummaryLabel = new Label();
            mRuntimeSummaryLabel.AddToClassList("toolbar-label");
            toolbar.Add(mRuntimeSummaryLabel);
            
            var refreshBtn = CreateToolbarButton("刷新", RefreshRuntimeTree);
            toolbar.Add(refreshBtn);
            
            // 树状图滚动视图
            mRuntimeScrollView = new ScrollView();
            mRuntimeScrollView.style.flexGrow = 1;
            container.Add(mRuntimeScrollView);
            
            return container;
        }

        private void RefreshRuntimeTree()
        {
            mRuntimeScrollView.Clear();
            
            if (!IsPlaying)
            {
                mRuntimeSummaryLabel.text = "未运行";
                mRuntimeScrollView.Add(CreateEmptyState("请先运行游戏以查看运行时事件"));
                return;
            }
            
            var totalListeners = 0;
            
            // Enum 事件
            var enumEvents = EventKit.Enum.GetAllEvents();
            if (enumEvents.Count > 0)
            {
                var enumFoldout = CreateRuntimeEventTypeFoldout("Enum", enumEvents, ref totalListeners);
                mRuntimeScrollView.Add(enumFoldout);
            }
            
            // Type 事件
            var typeEvents = EventKit.Type.GetAllEvents();
            if (typeEvents.Count > 0)
            {
                var typeFoldout = CreateRuntimeTypeFoldout(typeEvents, ref totalListeners);
                mRuntimeScrollView.Add(typeFoldout);
            }
            
            // String 事件
#pragma warning disable CS0618
            var stringEvents = EventKit.String.GetAllEvents();
#pragma warning restore CS0618
            if (stringEvents.Count > 0)
            {
                var stringFoldout = CreateRuntimeStringFoldout(stringEvents, ref totalListeners);
                mRuntimeScrollView.Add(stringFoldout);
            }
            
            mRuntimeSummaryLabel.text = $"共 {totalListeners} 个监听器";
            
            if (totalListeners == 0)
            {
                mRuntimeScrollView.Add(CreateEmptyState("暂无已注册的事件监听器"));
            }
        }

        private Foldout CreateRuntimeEventTypeFoldout(string eventType, IReadOnlyDictionary<EnumEventKey, EasyEvents> events, ref int totalListeners)
        {
            var eventCount = 0;
            foreach (var kvp in events)
                eventCount += GetTotalListenerCount(kvp.Value);
            totalListeners += eventCount;
            
            var foldoutKey = $"runtime_enum";
            var isExpanded = !mExpandedFoldouts.Contains(foldoutKey) || mExpandedFoldouts.Count == 0;
            
            var foldout = new Foldout { text = $"🟢 Enum 事件 ({eventCount} 监听器)", value = isExpanded };
            foldout.style.marginTop = 8;
            foldout.style.marginBottom = 4;
            foldout.style.borderLeftWidth = 4;
            foldout.style.borderLeftColor = new StyleColor(new UnityEngine.Color(0.3f, 0.7f, 0.3f));
            foldout.style.paddingLeft = 8;
            
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    mExpandedFoldouts.Remove(foldoutKey);
                else
                    mExpandedFoldouts.Add(foldoutKey);
            });
            
            // 按参数类型分组
            var byParamType = new Dictionary<string, List<(EnumEventKey key, string paramType, IEasyEvent evt, int count)>>();
            
            foreach (var kvp in events)
            {
                foreach (var innerKvp in kvp.Value.GetAllEvents())
                {
                    var paramType = innerKvp.Key.Name;
                    if (!byParamType.TryGetValue(paramType, out var list))
                    {
                        list = new List<(EnumEventKey, string, IEasyEvent, int)>();
                        byParamType[paramType] = list;
                    }
                    list.Add((kvp.Key, paramType, innerKvp.Value, innerKvp.Value.ListenerCount));
                }
            }
            
            foreach (var paramKvp in byParamType)
            {
                var paramFoldout = CreateRuntimeParamFoldout(paramKvp.Key, paramKvp.Value);
                foldout.Add(paramFoldout);
            }
            
            return foldout;
        }

        private Foldout CreateRuntimeParamFoldout(string paramType, List<(EnumEventKey key, string paramType, IEasyEvent evt, int count)> events)
        {
            var totalCount = 0;
            foreach (var e in events) totalCount += e.count;
            
            var (bgColor, borderColor, textColor) = GetParamTypeColors(paramType, 0);
            
            var foldoutKey = $"runtime_enum_param_{paramType}";
            var isExpanded = !mExpandedFoldouts.Contains(foldoutKey);
            
            var foldout = new Foldout { text = $"📦 通道 <{paramType}> ({totalCount} 监听器)", value = isExpanded };
            foldout.style.marginLeft = 12;
            foldout.style.marginTop = 4;
            foldout.style.marginBottom = 4;
            foldout.style.backgroundColor = new StyleColor(bgColor);
            foldout.style.borderLeftWidth = 3;
            foldout.style.borderLeftColor = new StyleColor(borderColor);
            foldout.style.paddingLeft = 8;
            foldout.style.paddingTop = 4;
            foldout.style.paddingBottom = 4;
            foldout.style.borderTopLeftRadius = 4;
            foldout.style.borderTopRightRadius = 4;
            foldout.style.borderBottomLeftRadius = 4;
            foldout.style.borderBottomRightRadius = 4;
            
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    mExpandedFoldouts.Remove(foldoutKey);
                else
                    mExpandedFoldouts.Add(foldoutKey);
            });
            
            foreach (var e in events)
            {
                if (e.count == 0) continue;
                
                var enumName = Enum.GetName(e.key.EnumType, e.key.EnumValue) ?? e.key.EnumValue.ToString();
                var keyNode = CreateRuntimeEventKeyNode($"{e.key.EnumType.Name}.{enumName}", e.evt);
                foldout.Add(keyNode);
            }
            
            return foldout;
        }

        private Foldout CreateRuntimeTypeFoldout(IReadOnlyDictionary<Type, IEasyEvent> events, ref int totalListeners)
        {
            var eventCount = 0;
            foreach (var kvp in events)
                eventCount += kvp.Value.ListenerCount;
            totalListeners += eventCount;
            
            var foldoutKey = $"runtime_type";
            var isExpanded = !mExpandedFoldouts.Contains(foldoutKey);
            
            var foldout = new Foldout { text = $"🔵 Type 事件 ({eventCount} 监听器)", value = isExpanded };
            foldout.style.marginTop = 8;
            foldout.style.marginBottom = 4;
            foldout.style.borderLeftWidth = 4;
            foldout.style.borderLeftColor = new StyleColor(new UnityEngine.Color(0.3f, 0.5f, 0.9f));
            foldout.style.paddingLeft = 8;
            
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    mExpandedFoldouts.Remove(foldoutKey);
                else
                    mExpandedFoldouts.Add(foldoutKey);
            });
            
            foreach (var kvp in events)
            {
                if (kvp.Value.ListenerCount == 0) continue;
                
                var (bgColor, borderColor, _) = GetParamTypeColors(kvp.Key.Name, 0);
                
                var typeContainer = new VisualElement();
                typeContainer.style.marginLeft = 12;
                typeContainer.style.marginTop = 4;
                typeContainer.style.marginBottom = 4;
                typeContainer.style.backgroundColor = new StyleColor(bgColor);
                typeContainer.style.borderLeftWidth = 3;
                typeContainer.style.borderLeftColor = new StyleColor(borderColor);
                typeContainer.style.paddingLeft = 8;
                typeContainer.style.paddingTop = 4;
                typeContainer.style.paddingBottom = 4;
                typeContainer.style.borderTopLeftRadius = 4;
                typeContainer.style.borderTopRightRadius = 4;
                typeContainer.style.borderBottomLeftRadius = 4;
                typeContainer.style.borderBottomRightRadius = 4;
                
                var keyNode = CreateRuntimeEventKeyNode($"📦 {kvp.Key.Name}", kvp.Value);
                typeContainer.Add(keyNode);
                foldout.Add(typeContainer);
            }
            
            return foldout;
        }

        private Foldout CreateRuntimeStringFoldout(IReadOnlyDictionary<string, EasyEvents> events, ref int totalListeners)
        {
            var eventCount = 0;
            foreach (var kvp in events)
                eventCount += GetTotalListenerCount(kvp.Value);
            totalListeners += eventCount;
            
            var foldoutKey = $"runtime_string";
            var isExpanded = !mExpandedFoldouts.Contains(foldoutKey);
            
            var foldout = new Foldout { text = $"🟠 String 事件 ({eventCount} 监听器) ⚠️已过时", value = isExpanded };
            foldout.style.marginTop = 8;
            foldout.style.marginBottom = 4;
            foldout.style.borderLeftWidth = 4;
            foldout.style.borderLeftColor = new StyleColor(new UnityEngine.Color(0.9f, 0.6f, 0.2f));
            foldout.style.paddingLeft = 8;
            
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    mExpandedFoldouts.Remove(foldoutKey);
                else
                    mExpandedFoldouts.Add(foldoutKey);
            });
            
            // 按参数类型分组
            var byParamType = new Dictionary<string, List<(string key, string paramType, IEasyEvent evt, int count)>>();
            
            foreach (var kvp in events)
            {
                foreach (var innerKvp in kvp.Value.GetAllEvents())
                {
                    var paramType = innerKvp.Key.Name;
                    if (!byParamType.TryGetValue(paramType, out var list))
                    {
                        list = new List<(string, string, IEasyEvent, int)>();
                        byParamType[paramType] = list;
                    }
                    list.Add((kvp.Key, paramType, innerKvp.Value, innerKvp.Value.ListenerCount));
                }
            }
            
            foreach (var paramKvp in byParamType)
            {
                var (bgColor, borderColor, textColor) = GetParamTypeColors(paramKvp.Key, 0);
                
                var paramContainer = new VisualElement();
                paramContainer.style.marginLeft = 12;
                paramContainer.style.marginTop = 4;
                paramContainer.style.marginBottom = 4;
                paramContainer.style.backgroundColor = new StyleColor(bgColor);
                paramContainer.style.borderLeftWidth = 3;
                paramContainer.style.borderLeftColor = new StyleColor(borderColor);
                paramContainer.style.paddingLeft = 8;
                paramContainer.style.paddingTop = 4;
                paramContainer.style.paddingBottom = 4;
                paramContainer.style.borderTopLeftRadius = 4;
                paramContainer.style.borderTopRightRadius = 4;
                paramContainer.style.borderBottomLeftRadius = 4;
                paramContainer.style.borderBottomRightRadius = 4;
                
                var paramHeader = new Label($"📦 通道 <{paramKvp.Key}>");
                paramHeader.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                paramHeader.style.marginBottom = 4;
                paramHeader.style.color = new StyleColor(textColor);
                paramContainer.Add(paramHeader);
                
                foreach (var e in paramKvp.Value)
                {
                    if (e.count == 0) continue;
                    var keyNode = CreateRuntimeEventKeyNode($"\"{e.key}\"", e.evt);
                    paramContainer.Add(keyNode);
                }
                
                foldout.Add(paramContainer);
            }
            
            return foldout;
        }

        private VisualElement CreateRuntimeEventKeyNode(string eventKey, IEasyEvent evt)
        {
            var container = new VisualElement();
            container.style.marginLeft = 8;
            container.style.marginTop = 4;
            container.style.marginBottom = 4;
            container.style.paddingLeft = 8;
            container.style.borderLeftWidth = 2;
            container.style.borderLeftColor = new StyleColor(new UnityEngine.Color(0.5f, 0.5f, 0.5f));
            
            // 事件键标题
            var keyHeader = new Label($"🔑 {eventKey} ({evt.ListenerCount} 监听器)");
            keyHeader.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            keyHeader.style.fontSize = 12;
            keyHeader.style.marginBottom = 4;
            container.Add(keyHeader);
            
            // 监听器列表
            var listenersContainer = new VisualElement();
            listenersContainer.style.marginLeft = 16;
            listenersContainer.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.2f, 0.3f, 0.2f, 0.4f));
            listenersContainer.style.borderLeftWidth = 3;
            listenersContainer.style.borderLeftColor = new StyleColor(new UnityEngine.Color(0.3f, 0.8f, 0.4f));
            listenersContainer.style.paddingLeft = 8;
            listenersContainer.style.paddingTop = 4;
            listenersContainer.style.paddingBottom = 4;
            listenersContainer.style.borderTopLeftRadius = 4;
            listenersContainer.style.borderTopRightRadius = 4;
            listenersContainer.style.borderBottomLeftRadius = 4;
            listenersContainer.style.borderBottomRightRadius = 4;
            
            var listenerHeader = new Label($"📡 监听器 ({evt.ListenerCount})");
            listenerHeader.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            listenerHeader.style.fontSize = 11;
            listenerHeader.style.marginBottom = 2;
            listenersContainer.Add(listenerHeader);
            
            var index = 0;
            foreach (var del in evt.GetListeners())
            {
                var item = CreateRuntimeListenerItem(index++, del);
                listenersContainer.Add(item);
            }
            
            container.Add(listenersContainer);
            return container;
        }

        private VisualElement CreateRuntimeListenerItem(int index, Delegate del)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.marginTop = 2;
            item.style.marginBottom = 2;
            
            var targetType = del.Target?.GetType().Name ?? del.Method?.DeclaringType?.Name ?? "Unknown";
            var methodName = del.Method?.Name ?? "Unknown";
            
            var label = new Label($"  #{index + 1} {targetType}.{methodName}");
            label.style.flexGrow = 1;
            label.style.fontSize = 10;
            label.style.color = new StyleColor(new UnityEngine.Color(0.8f, 0.8f, 0.8f));
            item.Add(label);
            
            // 如果有调试信息，显示跳转按钮
            if (EasyEventDebugger.TryGetDebugInfo(del, out var debugInfo) && !string.IsNullOrEmpty(debugInfo.FilePath))
            {
                var jumpBtn = new Button(() => OpenFileAtLine(debugInfo.FilePath, debugInfo.LineNumber)) { text = "→" };
                jumpBtn.style.width = 24;
                jumpBtn.style.height = 16;
                jumpBtn.style.fontSize = 10;
                jumpBtn.style.paddingLeft = 0;
                jumpBtn.style.paddingRight = 0;
                item.Add(jumpBtn);
            }
            
            return item;
        }

        private static int GetTotalListenerCount(EasyEvents events)
        {
            var count = 0;
            foreach (var kvp in events.GetAllEvents())
                count += kvp.Value.ListenerCount;
            return count;
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
            
            mScanSummaryLabel = new Label();
            mScanSummaryLabel.AddToClassList("toolbar-label");
            toolbar.Add(mScanSummaryLabel);
            
            var scanBtn = CreateToolbarButton("扫描", () =>
            {
                mScanResults.Clear();
                mScanResults.AddRange(EventCodeScanner.ScanFolder(mScanFolder, true));
                RefreshScanResultsTree();
            });
            toolbar.Add(scanBtn);
            
            // 树状图滚动视图
            mScanResultsScrollView = new ScrollView();
            mScanResultsScrollView.style.flexGrow = 1;
            container.Add(mScanResultsScrollView);
            
            return container;
        }

        /// <summary>
        /// 构建树状图结构展示扫描结果
        /// 层级：事件类型 -> 参数类型(通道) -> 事件键 -> Send/Register -> 代码位置
        /// </summary>
        private void RefreshScanResultsTree()
        {
            mScanResultsScrollView.Clear();
            
            if (mScanResults.Count == 0)
            {
                mScanSummaryLabel.text = "无结果";
                mScanResultsScrollView.Add(CreateEmptyState("点击「扫描」按钮开始扫描代码"));
                return;
            }
            
            // 统计
            var enumCount = 0;
            var typeCount = 0;
            var stringCount = 0;
            foreach (var result in mScanResults)
            {
                switch (result.EventType)
                {
                    case "Enum": enumCount++; break;
                    case "Type": typeCount++; break;
                    case "String": stringCount++; break;
                }
            }
            mScanSummaryLabel.text = $"共 {mScanResults.Count} 处 (Enum:{enumCount} Type:{typeCount} String:{stringCount})";
            
            // 构建树结构: EventType -> ParamType -> EventKey -> CallType -> Results
            var tree = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, List<EventCodeScanner.ScanResult>>>>>();
            
            foreach (var result in mScanResults)
            {
                var eventType = result.EventType;
                var paramType = result.ParamType ?? "void";
                var eventKey = result.EventKey;
                var callType = result.CallType;
                
                if (!tree.TryGetValue(eventType, out var paramDict))
                {
                    paramDict = new Dictionary<string, Dictionary<string, Dictionary<string, List<EventCodeScanner.ScanResult>>>>();
                    tree[eventType] = paramDict;
                }
                
                if (!paramDict.TryGetValue(paramType, out var keyDict))
                {
                    keyDict = new Dictionary<string, Dictionary<string, List<EventCodeScanner.ScanResult>>>();
                    paramDict[paramType] = keyDict;
                }
                
                if (!keyDict.TryGetValue(eventKey, out var callDict))
                {
                    callDict = new Dictionary<string, List<EventCodeScanner.ScanResult>>();
                    keyDict[eventKey] = callDict;
                }
                
                if (!callDict.TryGetValue(callType, out var list))
                {
                    list = new List<EventCodeScanner.ScanResult>();
                    callDict[callType] = list;
                }
                
                list.Add(result);
            }
            
            // 渲染树
            var eventTypeOrder = new[] { "Enum", "Type", "String" };
            foreach (var eventType in eventTypeOrder)
            {
                if (!tree.TryGetValue(eventType, out var paramDict)) continue;
                
                var eventTypeFoldout = CreateEventTypeFoldout(eventType, paramDict);
                mScanResultsScrollView.Add(eventTypeFoldout);
            }
        }

        private Foldout CreateEventTypeFoldout(string eventType, Dictionary<string, Dictionary<string, Dictionary<string, List<EventCodeScanner.ScanResult>>>> paramDict)
        {
            var totalCount = 0;
            foreach (var p in paramDict.Values)
                foreach (var k in p.Values)
                    foreach (var c in k.Values)
                        totalCount += c.Count;
            
            var icon = eventType switch
            {
                "Enum" => "🟢",
                "Type" => "🔵",
                "String" => "🟠",
                _ => "⚪"
            };
            
            var foldout = new Foldout { text = $"{icon} {eventType} 事件 ({totalCount})", value = true };
            foldout.style.marginTop = 8;
            foldout.style.marginBottom = 4;
            
            var (_, borderColor, _) = GetEventTypeColors(eventType);
            foldout.style.borderLeftWidth = 4;
            foldout.style.borderLeftColor = new StyleColor(borderColor);
            foldout.style.paddingLeft = 8;
            
            foreach (var paramKvp in paramDict)
            {
                var paramFoldout = CreateParamTypeFoldout(eventType, paramKvp.Key, paramKvp.Value);
                foldout.Add(paramFoldout);
            }
            
            return foldout;
        }

        private Foldout CreateParamTypeFoldout(string eventType, string paramType, Dictionary<string, Dictionary<string, List<EventCodeScanner.ScanResult>>> keyDict)
        {
            var totalCount = 0;
            foreach (var k in keyDict.Values)
                foreach (var c in k.Values)
                    totalCount += c.Count;
            
            var (bgColor, borderColor, textColor) = GetParamTypeColors(paramType, 0);
            
            var foldout = new Foldout { text = $"📦 通道 <{paramType}> ({totalCount})", value = true };
            foldout.style.marginLeft = 12;
            foldout.style.marginTop = 4;
            foldout.style.marginBottom = 4;
            foldout.style.backgroundColor = new StyleColor(bgColor);
            foldout.style.borderLeftWidth = 3;
            foldout.style.borderLeftColor = new StyleColor(borderColor);
            foldout.style.paddingLeft = 8;
            foldout.style.paddingTop = 4;
            foldout.style.paddingBottom = 4;
            foldout.style.borderTopLeftRadius = 4;
            foldout.style.borderTopRightRadius = 4;
            foldout.style.borderBottomLeftRadius = 4;
            foldout.style.borderBottomRightRadius = 4;
            
            foreach (var keyKvp in keyDict)
            {
                var keyNode = CreateEventKeyNode(keyKvp.Key, keyKvp.Value);
                foldout.Add(keyNode);
            }
            
            return foldout;
        }

        private VisualElement CreateEventKeyNode(string eventKey, Dictionary<string, List<EventCodeScanner.ScanResult>> callDict)
        {
            var container = new VisualElement();
            container.style.marginLeft = 8;
            container.style.marginTop = 4;
            container.style.marginBottom = 4;
            container.style.paddingLeft = 8;
            container.style.borderLeftWidth = 2;
            container.style.borderLeftColor = new StyleColor(new UnityEngine.Color(0.5f, 0.5f, 0.5f));
            
            // 事件键标题
            var keyHeader = new Label($"🔑 {eventKey}");
            keyHeader.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            keyHeader.style.fontSize = 12;
            keyHeader.style.marginBottom = 4;
            container.Add(keyHeader);
            
            // 发送方和接收方并排显示
            var rowContainer = new VisualElement();
            rowContainer.style.flexDirection = FlexDirection.Row;
            rowContainer.style.marginLeft = 16;
            container.Add(rowContainer);
            
            // 发送方 (Send)
            var sendContainer = CreateCallTypeColumn("📤 发送方", "Send", callDict);
            sendContainer.style.flexGrow = 1;
            sendContainer.style.marginRight = 8;
            rowContainer.Add(sendContainer);
            
            // 接收方 (Register)
            var registerContainer = CreateCallTypeColumn("📡 接收方", "Register", callDict);
            registerContainer.style.flexGrow = 1;
            rowContainer.Add(registerContainer);
            
            // 注销 (UnRegister) - 如果有的话单独显示
            if (callDict.ContainsKey("UnRegister"))
            {
                var unregisterContainer = CreateCallTypeColumn("🔕 注销", "UnRegister", callDict);
                unregisterContainer.style.marginLeft = 16;
                unregisterContainer.style.marginTop = 4;
                container.Add(unregisterContainer);
            }
            
            return container;
        }

        private VisualElement CreateCallTypeColumn(string title, string callType, Dictionary<string, List<EventCodeScanner.ScanResult>> callDict)
        {
            var container = new VisualElement();
            container.style.minWidth = 200;
            
            var (bgColor, borderColor) = callType switch
            {
                "Send" => (new UnityEngine.Color(0.3f, 0.2f, 0.2f, 0.4f), new UnityEngine.Color(0.9f, 0.5f, 0.3f)),
                "Register" => (new UnityEngine.Color(0.2f, 0.3f, 0.2f, 0.4f), new UnityEngine.Color(0.3f, 0.8f, 0.4f)),
                "UnRegister" => (new UnityEngine.Color(0.25f, 0.25f, 0.25f, 0.4f), new UnityEngine.Color(0.6f, 0.6f, 0.6f)),
                _ => (new UnityEngine.Color(0.2f, 0.2f, 0.2f, 0.4f), new UnityEngine.Color(0.5f, 0.5f, 0.5f))
            };
            
            container.style.backgroundColor = new StyleColor(bgColor);
            container.style.borderLeftWidth = 3;
            container.style.borderLeftColor = new StyleColor(borderColor);
            container.style.paddingLeft = 8;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 4;
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            
            if (!callDict.TryGetValue(callType, out var results) || results.Count == 0)
            {
                var header = new Label($"{title} (0)");
                header.style.color = new StyleColor(new UnityEngine.Color(0.5f, 0.5f, 0.5f));
                header.style.fontSize = 11;
                container.Add(header);
                
                var empty = new Label("  无");
                empty.style.color = new StyleColor(new UnityEngine.Color(0.4f, 0.4f, 0.4f));
                empty.style.fontSize = 10;
                container.Add(empty);
            }
            else
            {
                var header = new Label($"{title} ({results.Count})");
                header.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                header.style.fontSize = 11;
                header.style.marginBottom = 2;
                container.Add(header);
                
                foreach (var result in results)
                {
                    var item = CreateTreeResultItem(result);
                    container.Add(item);
                }
            }
            
            return container;
        }

        private VisualElement CreateTreeResultItem(EventCodeScanner.ScanResult result)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.marginTop = 2;
            item.style.marginBottom = 2;
            
            var path = new Label();
            path.style.flexGrow = 1;
            path.style.fontSize = 10;
            path.style.color = new StyleColor(new UnityEngine.Color(0.7f, 0.7f, 0.7f));
            
            // 提取文件名
            var fileName = System.IO.Path.GetFileName(result.FilePath);
            path.text = $"  {fileName}:{result.LineNumber}";
            item.Add(path);
            
            var jumpBtn = new Button(() => OpenFileAtLine(result.FilePath, result.LineNumber)) { text = "→" };
            jumpBtn.style.width = 24;
            jumpBtn.style.height = 16;
            jumpBtn.style.fontSize = 10;
            jumpBtn.style.paddingLeft = 0;
            jumpBtn.style.paddingRight = 0;
            item.Add(jumpBtn);
            
            return item;
        }

        private static (UnityEngine.Color bg, UnityEngine.Color border, UnityEngine.Color text) GetEventTypeColors(string eventType)
        {
            return eventType switch
            {
                "Enum" => (
                    new UnityEngine.Color(0.15f, 0.25f, 0.15f, 0.5f),
                    new UnityEngine.Color(0.3f, 0.7f, 0.3f),
                    new UnityEngine.Color(0.6f, 0.9f, 0.6f)
                ),
                "Type" => (
                    new UnityEngine.Color(0.15f, 0.2f, 0.3f, 0.5f),
                    new UnityEngine.Color(0.3f, 0.5f, 0.9f),
                    new UnityEngine.Color(0.6f, 0.7f, 1f)
                ),
                "String" => (
                    new UnityEngine.Color(0.3f, 0.2f, 0.1f, 0.5f),
                    new UnityEngine.Color(0.9f, 0.6f, 0.2f),
                    new UnityEngine.Color(1f, 0.8f, 0.4f)
                ),
                _ => (
                    new UnityEngine.Color(0.2f, 0.2f, 0.2f, 0.5f),
                    new UnityEngine.Color(0.5f, 0.5f, 0.5f),
                    new UnityEngine.Color(0.8f, 0.8f, 0.8f)
                )
            };
        }

        private static (UnityEngine.Color bg, UnityEngine.Color border, UnityEngine.Color text) GetParamTypeColors(string paramType, int index)
        {
            return paramType.ToLower() switch
            {
                "void" => (
                    new UnityEngine.Color(0.2f, 0.25f, 0.2f, 0.5f),
                    new UnityEngine.Color(0.4f, 0.7f, 0.4f),
                    new UnityEngine.Color(0.6f, 0.9f, 0.6f)
                ),
                "int" => (
                    new UnityEngine.Color(0.2f, 0.22f, 0.3f, 0.5f),
                    new UnityEngine.Color(0.4f, 0.5f, 0.9f),
                    new UnityEngine.Color(0.6f, 0.7f, 1f)
                ),
                "float" => (
                    new UnityEngine.Color(0.3f, 0.25f, 0.2f, 0.5f),
                    new UnityEngine.Color(0.9f, 0.6f, 0.3f),
                    new UnityEngine.Color(1f, 0.8f, 0.5f)
                ),
                "string" => (
                    new UnityEngine.Color(0.3f, 0.2f, 0.25f, 0.5f),
                    new UnityEngine.Color(0.8f, 0.4f, 0.7f),
                    new UnityEngine.Color(1f, 0.6f, 0.9f)
                ),
                "bool" => (
                    new UnityEngine.Color(0.25f, 0.2f, 0.2f, 0.5f),
                    new UnityEngine.Color(0.9f, 0.4f, 0.4f),
                    new UnityEngine.Color(1f, 0.6f, 0.6f)
                ),
                _ when paramType.Contains("(") => (
                    new UnityEngine.Color(0.25f, 0.25f, 0.2f, 0.5f),
                    new UnityEngine.Color(0.9f, 0.8f, 0.3f),
                    new UnityEngine.Color(1f, 0.95f, 0.5f)
                ),
                _ => (
                    new UnityEngine.Color(0.2f, 0.25f, 0.28f, 0.5f),
                    new UnityEngine.Color(0.3f, 0.8f, 0.8f),
                    new UnityEngine.Color(0.5f, 0.95f, 0.95f)
                )
            };
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
                    RefreshRuntimeTree();
                else if (mViewMode == ViewMode.History)
                    RefreshHistoryList();
                    
                mLastRefreshTime = EditorApplication.timeSinceStartup;
            }
        }

        #endregion

        private static void OpenFileAtLine(string filePath, int line)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
            if (asset != null)
                AssetDatabase.OpenAsset(asset, line);
        }
    }
}
