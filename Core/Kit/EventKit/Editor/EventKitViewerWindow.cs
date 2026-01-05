using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 事件可视化查看器
    /// </summary>
    public class EventKitViewerWindow : EditorWindow
    {
        private const string WINDOW_TITLE = "EventKit Viewer";
        private const float REFRESH_INTERVAL = 0.5f;

        private enum ViewMode { Runtime, History, CodeScan }
        private enum EventCategory { Enum, Type, String }

        private ViewMode mViewMode = ViewMode.Runtime;
        private Vector2 mLeftScrollPos;
        private Vector2 mRightScrollPos;
        private Vector2 mHistoryScrollPos;
        private EventCategory mSelectedCategory = EventCategory.Enum;
        private string mSelectedEventKey;
        private double mLastRefreshTime;

        // 运行时数据缓存
        private readonly List<EventNodeData> mCachedNodes = new();
        private readonly List<ListenerDisplayData> mCachedListeners = new();

        // 代码扫描数据
        private string mScanFolder = "Assets/Scripts";
        private readonly List<EventCodeScanner.ScanResult> mScanResults = new();
        private string mScanFilterType = "All";
        private string mScanFilterCall = "All";

        // 历史过滤
        private string mHistoryFilterAction = "All";
        private string mHistoryFilterType = "All";
        private bool mHistoryAutoScroll = true;
        private bool mClearHistoryOnStop = true;

        [MenuItem("YokiFrame/EventKit/Event Viewer")]
        private static void Open()
        {
            var window = GetWindow<EventKitViewerWindow>(false, WINDOW_TITLE);
            window.minSize = new Vector2(900, 500);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            mScanFolder = EditorPrefs.GetString("EventKitViewer_ScanFolder", "Assets/Scripts");
            mClearHistoryOnStop = EditorPrefs.GetBool("EventKitViewer_ClearHistoryOnStop", true);
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorPrefs.SetString("EventKitViewer_ScanFolder", mScanFolder);
            EditorPrefs.SetBool("EventKitViewer_ClearHistoryOnStop", mClearHistoryOnStop);
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                mCachedNodes.Clear();
                mCachedListeners.Clear();
                mSelectedEventKey = null;
                Repaint();
            }
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            DrawMainToolbar();

            switch (mViewMode)
            {
                case ViewMode.Runtime:
                    DrawRuntimeView();
                    break;
                case ViewMode.History:
                    DrawHistoryView();
                    break;
                case ViewMode.CodeScan:
                    DrawCodeScanView();
                    break;
            }
        }

        private void DrawMainToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Toggle(mViewMode == ViewMode.Runtime, "运行时监控", EditorStyles.toolbarButton, GUILayout.Width(80)))
                mViewMode = ViewMode.Runtime;

            if (GUILayout.Toggle(mViewMode == ViewMode.History, "事件历史", EditorStyles.toolbarButton, GUILayout.Width(70)))
                mViewMode = ViewMode.History;

            if (GUILayout.Toggle(mViewMode == ViewMode.CodeScan, "代码扫描", EditorStyles.toolbarButton, GUILayout.Width(70)))
                mViewMode = ViewMode.CodeScan;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        #region History View

        private void DrawHistoryView()
        {
            DrawHistoryToolbar();
            DrawHistoryList();
        }

        private void DrawHistoryToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUILayout.LabelField("操作:", GUILayout.Width(35));
            mHistoryFilterAction = DrawFilterDropdown(mHistoryFilterAction, new[] { "All", "Register", "UnRegister", "Send" });

            EditorGUILayout.LabelField("类型:", GUILayout.Width(35));
            mHistoryFilterType = DrawFilterDropdown(mHistoryFilterType, new[] { "All", "Enum", "Type", "String", "Listener" });

            GUILayout.Space(10);
            
            var recordSend = GUILayout.Toggle(EasyEventDebugger.RecordSendEvents, "记录Send", EditorStyles.toolbarButton, GUILayout.Width(70));
            if (recordSend != EasyEventDebugger.RecordSendEvents)
                EasyEventDebugger.RecordSendEvents = recordSend;
            
            // 只有开启记录Send时才显示堆栈选项
            if (EasyEventDebugger.RecordSendEvents)
            {
                var recordStack = GUILayout.Toggle(EasyEventDebugger.RecordSendStackTrace, "堆栈", EditorStyles.toolbarButton, GUILayout.Width(45));
                if (recordStack != EasyEventDebugger.RecordSendStackTrace)
                    EasyEventDebugger.RecordSendStackTrace = recordStack;
            }
            
            mHistoryAutoScroll = GUILayout.Toggle(mHistoryAutoScroll, "自动滚动", EditorStyles.toolbarButton, GUILayout.Width(70));
            
            var newClearOnStop = GUILayout.Toggle(mClearHistoryOnStop, "停止时清空", EditorStyles.toolbarButton, GUILayout.Width(75));
            if (newClearOnStop != mClearHistoryOnStop)
            {
                mClearHistoryOnStop = newClearOnStop;
                EditorPrefs.SetBool("EventKitViewer_ClearHistoryOnStop", mClearHistoryOnStop);
            }

            GUILayout.FlexibleSpace();

            var history = EasyEventDebugger.EventHistory;
            EditorGUILayout.LabelField($"记录: {history.Count}/{EasyEventDebugger.MAX_HISTORY_COUNT}", GUILayout.Width(100));

            if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(45)))
                EasyEventDebugger.ClearHistory();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHistoryList()
        {
            var history = EasyEventDebugger.EventHistory;

            mHistoryScrollPos = EditorGUILayout.BeginScrollView(mHistoryScrollPos, "box", GUILayout.ExpandHeight(true));

            if (history.Count == 0)
            {
                EditorGUILayout.HelpBox("暂无事件记录，事件触发后将在此显示", MessageType.Info);
            }
            else
            {
                for (var i = history.Count - 1; i >= 0; i--)
                {
                    var entry = history[i];
                    if (!PassHistoryFilter(entry)) continue;
                    DrawHistoryEntry(entry);
                }
            }

            EditorGUILayout.EndScrollView();

            // 自动滚动到顶部（最新记录）
            if (mHistoryAutoScroll && Event.current.type == EventType.Repaint)
            {
                var lastCount = EditorPrefs.GetInt("EventKitViewer_LastHistoryCount", 0);
                if (history.Count > lastCount)
                {
                    mHistoryScrollPos = Vector2.zero;
                    EditorPrefs.SetInt("EventKitViewer_LastHistoryCount", history.Count);
                }
            }
        }

        private bool PassHistoryFilter(EasyEventDebugger.EventHistoryEntry entry)
        {
            if (mHistoryFilterAction != "All" && entry.Action != mHistoryFilterAction) return false;
            if (mHistoryFilterType != "All" && entry.EventType != mHistoryFilterType) return false;
            return true;
        }

        private void DrawHistoryEntry(EasyEventDebugger.EventHistoryEntry entry)
        {
            EditorGUILayout.BeginHorizontal("helpBox");

            // 时间
            EditorGUILayout.LabelField($"{entry.Time:F2}s", GUILayout.Width(55));

            // 操作类型颜色
            var actionColor = entry.Action switch
            {
                "Register" => new Color(0.5f, 1f, 0.5f),
                "UnRegister" => new Color(1f, 0.5f, 0.5f),
                "Send" => new Color(0.5f, 0.8f, 1f),
                _ => Color.white
            };

            var typeColor = entry.EventType switch
            {
                "Enum" => new Color(0.4f, 0.7f, 1f),
                "Type" => new Color(0.5f, 0.9f, 0.5f),
                "String" => new Color(1f, 0.8f, 0.4f),
                _ => new Color(0.8f, 0.8f, 0.8f)
            };

            var oldBg = GUI.backgroundColor;

            GUI.backgroundColor = actionColor;
            GUILayout.Label(entry.Action, "CN CountBadge", GUILayout.Width(70));

            GUI.backgroundColor = typeColor;
            GUILayout.Label(entry.EventType, "CN CountBadge", GUILayout.Width(55));

            GUI.backgroundColor = oldBg;

            // 事件键
            EditorGUILayout.LabelField(entry.EventKey, EditorStyles.boldLabel, GUILayout.Width(180));

            // 参数
            if (!string.IsNullOrEmpty(entry.Args))
                EditorGUILayout.LabelField($"({entry.Args})", EditorStyles.miniLabel, GUILayout.Width(120));

            // 调用位置
            if (!string.IsNullOrEmpty(entry.CallerInfo))
            {
                var shortPath = entry.CallerInfo;
                if (shortPath.Length > 35)
                    shortPath = "..." + shortPath[^32..];
                EditorGUILayout.LabelField(shortPath, EditorStyles.miniLabel);

                if (GUILayout.Button("跳转", GUILayout.Width(40)))
                {
                    var parts = entry.CallerInfo.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1], out var line))
                        OpenFileAtLine(parts[0], line);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Runtime View

        private void DrawRuntimeView()
        {
            DrawRuntimeToolbar();

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.Space(50);
                EditorGUILayout.LabelField("请进入 Play Mode 查看运行时事件注册情况",
                    new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 14 },
                    GUILayout.ExpandHeight(true));
                return;
            }

            if (EditorApplication.timeSinceStartup - mLastRefreshTime > REFRESH_INTERVAL)
            {
                RefreshEventData();
                mLastRefreshTime = EditorApplication.timeSinceStartup;
            }

            EditorGUILayout.BeginHorizontal();
            DrawLeftPanel();
            DrawRightPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRuntimeToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("事件类型:", GUILayout.Width(60));

            if (DrawCategoryButton(EventCategory.Enum, "Enum"))
                SwitchCategory(EventCategory.Enum);
            if (DrawCategoryButton(EventCategory.Type, "Type"))
                SwitchCategory(EventCategory.Type);
            if (DrawCategoryButton(EventCategory.String, "String"))
                SwitchCategory(EventCategory.String);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50)))
                RefreshEventData();

            EditorGUILayout.EndHorizontal();
        }

        private bool DrawCategoryButton(EventCategory category, string label)
        {
            return GUILayout.Toggle(mSelectedCategory == category, label,
                EditorStyles.toolbarButton, GUILayout.Width(60)) && mSelectedCategory != category;
        }

        private void SwitchCategory(EventCategory category)
        {
            mSelectedCategory = category;
            mSelectedEventKey = null;
            RefreshEventData();
        }

        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(280));
            EditorGUILayout.LabelField($"已注册事件 ({mCachedNodes.Count})", EditorStyles.boldLabel);

            mLeftScrollPos = EditorGUILayout.BeginScrollView(mLeftScrollPos, "box", GUILayout.ExpandHeight(true));

            if (mCachedNodes.Count == 0)
                EditorGUILayout.LabelField("暂无注册事件", EditorStyles.centeredGreyMiniLabel);
            else
                foreach (var node in mCachedNodes)
                    DrawEventNode(node);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawEventNode(EventNodeData node)
        {
            var isSelected = mSelectedEventKey == node.Key;
            var rect = EditorGUILayout.BeginHorizontal();

            if (isSelected)
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.49f, 0.91f, 0.5f));

            var oldColor = GUI.color;
            GUI.color = node.ListenerCount > 0 ? Color.green : Color.gray;
            EditorGUILayout.LabelField("●", GUILayout.Width(15));
            GUI.color = oldColor;

            if (GUILayout.Button(node.DisplayName, EditorStyles.label))
            {
                mSelectedEventKey = node.Key;
                RefreshListenerData(node);
            }

            EditorGUILayout.LabelField($"[{node.ListenerCount}]", GUILayout.Width(35));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            if (string.IsNullOrEmpty(mSelectedEventKey))
            {
                EditorGUILayout.LabelField("监听器详情", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("选择左侧事件查看监听器详情", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"监听器详情 - {mSelectedEventKey}", EditorStyles.boldLabel);
                mRightScrollPos = EditorGUILayout.BeginScrollView(mRightScrollPos, "box", GUILayout.ExpandHeight(true));

                if (mCachedListeners.Count == 0)
                    EditorGUILayout.LabelField("暂无监听器", EditorStyles.centeredGreyMiniLabel);
                else
                    for (var i = 0; i < mCachedListeners.Count; i++)
                        DrawListenerInfo(i, mCachedListeners[i]);

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawListenerInfo(int index, ListenerDisplayData data)
        {
            EditorGUILayout.BeginVertical("helpBox");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"#{index + 1}", GUILayout.Width(25));
            EditorGUILayout.LabelField($"{data.TargetType}.{data.MethodName}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;

            if (!string.IsNullOrEmpty(data.FilePath))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"注册位置: {data.FilePath}:{data.LineNumber}");
                if (GUILayout.Button("跳转", GUILayout.Width(45)))
                    OpenFileAtLine(data.FilePath, data.LineNumber);
                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(data.StackTrace))
            {
                if (GUILayout.Button("查看堆栈", EditorStyles.miniButton, GUILayout.Width(70)))
                    Debug.Log($"[EventKit] {data.TargetType}.{data.MethodName} 注册堆栈:\n{data.StackTrace}");
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        #endregion

        #region Code Scan View

        private void DrawCodeScanView()
        {
            DrawScanToolbar();
            DrawScanResults();
        }

        private void DrawScanToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUILayout.LabelField("扫描目录:", GUILayout.Width(60));
            mScanFolder = EditorGUILayout.TextField(mScanFolder, GUILayout.Width(200));

            if (GUILayout.Button("...", EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                var folder = EditorUtility.OpenFolderPanel("选择扫描目录", mScanFolder, "");
                if (!string.IsNullOrEmpty(folder))
                {
                    var idx = folder.IndexOf("Assets", StringComparison.Ordinal);
                    mScanFolder = idx >= 0 ? folder[idx..] : folder;
                }
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField("类型:", GUILayout.Width(35));
            mScanFilterType = DrawFilterDropdown(mScanFilterType, new[] { "All", "Enum", "Type", "String" });
            EditorGUILayout.LabelField("调用:", GUILayout.Width(35));
            mScanFilterCall = DrawFilterDropdown(mScanFilterCall, new[] { "All", "Register", "Send" });

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("扫描", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                mScanResults.Clear();
                mScanResults.AddRange(EventCodeScanner.ScanFolder(mScanFolder, true));
            }

            EditorGUILayout.EndHorizontal();
        }

        private string DrawFilterDropdown(string current, string[] options)
        {
            var index = Array.IndexOf(options, current);
            if (index < 0) index = 0;
            return options[EditorGUILayout.Popup(index, options, EditorStyles.toolbarPopup, GUILayout.Width(70))];
        }

        private void DrawScanResults()
        {
            var filteredCount = 0;
            foreach (var r in mScanResults)
                if (PassScanFilter(r)) filteredCount++;

            EditorGUILayout.LabelField($"扫描结果 ({filteredCount} / {mScanResults.Count})", EditorStyles.boldLabel);
            mLeftScrollPos = EditorGUILayout.BeginScrollView(mLeftScrollPos, "box", GUILayout.ExpandHeight(true));

            if (mScanResults.Count == 0)
                EditorGUILayout.HelpBox("点击「扫描」按钮开始扫描项目代码", MessageType.Info);
            else
                foreach (var result in mScanResults)
                    if (PassScanFilter(result))
                        DrawScanResultItem(result);

            EditorGUILayout.EndScrollView();
        }

        private bool PassScanFilter(EventCodeScanner.ScanResult result)
        {
            if (mScanFilterType != "All" && result.EventType != mScanFilterType) return false;
            if (mScanFilterCall != "All" && result.CallType != mScanFilterCall) return false;
            return true;
        }

        private void DrawScanResultItem(EventCodeScanner.ScanResult result)
        {
            EditorGUILayout.BeginHorizontal("helpBox");

            var typeColor = result.EventType switch
            {
                "Enum" => new Color(0.4f, 0.7f, 1f),
                "Type" => new Color(0.5f, 0.9f, 0.5f),
                _ => new Color(1f, 0.8f, 0.4f)
            };
            var callColor = result.CallType == "Register" ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.6f, 0.4f);

            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = typeColor;
            GUILayout.Label(result.EventType, "CN CountBadge", GUILayout.Width(45));
            GUI.backgroundColor = callColor;
            GUILayout.Label(result.CallType, "CN CountBadge", GUILayout.Width(55));
            GUI.backgroundColor = oldBg;

            EditorGUILayout.LabelField(result.EventKey, EditorStyles.boldLabel, GUILayout.Width(150));

            var shortPath = result.FilePath.Length > 40 ? "..." + result.FilePath[^37..] : result.FilePath;
            EditorGUILayout.LabelField($"{shortPath}:{result.LineNumber}", EditorStyles.miniLabel);

            if (GUILayout.Button("跳转", GUILayout.Width(45)))
                OpenFileAtLine(result.FilePath, result.LineNumber);

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Data Refresh

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

            if (!string.IsNullOrEmpty(mSelectedEventKey))
            {
                var node = mCachedNodes.Find(n => n.Key == mSelectedEventKey);
                if (node.Key != null)
                    RefreshListenerData(node);
                else
                {
                    mSelectedEventKey = null;
                    mCachedListeners.Clear();
                }
            }
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
