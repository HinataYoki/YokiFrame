#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 的代码扫描视图。
    /// 使用三栏流向布局展示发送方、事件中心与接收方关系。
    /// </summary>
    public partial class EventKitToolPage
    {
        #region 健康状态枚举

        private enum HealthStatus
        {
            Healthy,
            Orphan,
            LeakRisk,
            NoSender
        }

        #endregion

        #region 代码扫描私有字段

        private TextField mScanFolderField;
        private TextField mSearchField;
        private ScrollView mScanResultsScrollView;
        private Label mScanSummaryLabel;
        private string mScanFolder = "Assets";
        private string mSearchKeyword = "";
        private bool mExcludeEditor = true;
        private readonly List<EventCodeScanner.ScanResult> mScanResults = new(256);
        private readonly List<VisualElement> mHighlightedElements = new(16);

        #endregion

        #region 事件流数据结构

        /// <summary>
        /// 单个事件的扫描聚合结果。
        /// </summary>
        private class EventFlowData
        {
            public string EventType;
            public string EventKey;
            public string ParamType;
            public List<EventCodeScanner.ScanResult> Senders = new(4);
            public List<EventCodeScanner.ScanResult> Receivers = new(4);
            public List<EventCodeScanner.ScanResult> Unregisters = new(4);

            public HealthStatus GetHealthStatus()
            {
                bool hasSenders = Senders.Count > 0;
                bool hasReceivers = Receivers.Count > 0;

                if (!hasSenders && hasReceivers)
                {
                    return HealthStatus.NoSender;
                }

                if (hasSenders && !hasReceivers)
                {
                    return HealthStatus.Orphan;
                }

                if (Receivers.Count > Unregisters.Count && Unregisters.Count > 0)
                {
                    return HealthStatus.LeakRisk;
                }

                return HealthStatus.Healthy;
            }
        }

        #endregion

        #region 构建代码扫描视图

        /// <summary>
        /// 创建代码扫描视图。
        /// 扫描结果区与快速导航区共享同一主内容区域，便于在大结果集下快速定位。
        /// </summary>
        private VisualElement CreateCodeScanView()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            container.style.overflow = Overflow.Hidden;

            var toolbar = CreateScanToolbar();
            container.Add(toolbar);

            var mainContent = new VisualElement();
            mainContent.style.flexDirection = FlexDirection.Row;
            mainContent.style.flexGrow = 1;
            mainContent.style.flexShrink = 1;
            mainContent.style.overflow = Overflow.Hidden;
            mainContent.RegisterCallback<GeometryChangedEvent>(OnCodeScanViewGeometryChanged);
            container.Add(mainContent);

            var resultsScrollView = new ScrollView(ScrollViewMode.Vertical);
            resultsScrollView.style.flexGrow = 2;
            resultsScrollView.style.flexBasis = 0;
            resultsScrollView.style.flexShrink = 1;
            resultsScrollView.style.paddingLeft = 16;
            resultsScrollView.style.paddingRight = 16;
            resultsScrollView.style.paddingTop = 16;
            resultsScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            mScanResultsScrollView = resultsScrollView;
            mainContent.Add(resultsScrollView);

            var quickNavPanel = CreateQuickNavPanel();
            mainContent.Add(quickNavPanel);

            return container;
        }

        /// <summary>
        /// 创建代码扫描工具栏。
        /// 工具栏负责目录选择、Editor 过滤、搜索和扫描摘要展示。
        /// </summary>
        private VisualElement CreateScanToolbar()
        {
            var toolbar = CreateToolbar();

            var folderLabel = new Label("扫描目录:");
            folderLabel.AddToClassList("toolbar-label");
            toolbar.Add(folderLabel);

            mScanFolderField = new TextField();
            mScanFolderField.value = mScanFolder;
            mScanFolderField.style.width = 200;
            mScanFolderField.RegisterValueChangedCallback(evt => mScanFolder = evt.newValue);
            toolbar.Add(mScanFolderField);

            var browseBtn = CreateToolbarButton("...", BrowseScanFolder);
            toolbar.Add(browseBtn);

            var excludeEditorToggle = CreateModernToggle("排除 Editor", mExcludeEditor, value => mExcludeEditor = value);
            excludeEditorToggle.style.marginLeft = 8;
            toolbar.Add(excludeEditorToggle);

            var searchIcon = new Image { image = EditorGUIUtility.IconContent("d_Search Icon").image };
            searchIcon.style.width = 14;
            searchIcon.style.height = 14;
            searchIcon.style.marginLeft = 16;
            searchIcon.tintColor = new Color(0.6f, 0.6f, 0.6f);
            toolbar.Add(searchIcon);

            mSearchField = new TextField();
            mSearchField.value = mSearchKeyword;
            mSearchField.style.width = 150;
            mSearchField.style.marginLeft = 4;
            const string placeholder = "搜索事件...";

            mSearchField.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                if (string.IsNullOrEmpty(mSearchField.value) || mSearchField.value == placeholder)
                {
                    SetTextFieldPlaceholderStyle(mSearchField, true);
                    mSearchField.SetValueWithoutNotify(placeholder);
                }
            });
            mSearchField.RegisterCallback<FocusInEvent>(_ =>
            {
                if (mSearchField.value == placeholder)
                {
                    mSearchField.SetValueWithoutNotify("");
                    SetTextFieldPlaceholderStyle(mSearchField, false);
                }
            });
            mSearchField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (string.IsNullOrEmpty(mSearchField.value))
                {
                    SetTextFieldPlaceholderStyle(mSearchField, true);
                    mSearchField.SetValueWithoutNotify(placeholder);
                }
            });
            mSearchField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != placeholder)
                {
                    mSearchKeyword = evt.newValue;
                    PerformSearch(mSearchKeyword);
                }
            });
            toolbar.Add(mSearchField);

            toolbar.Add(CreateToolbarSpacer());

            mScanSummaryLabel = new Label();
            mScanSummaryLabel.AddToClassList("toolbar-label");
            toolbar.Add(mScanSummaryLabel);

            var scanBtn = CreateToolbarButtonWithIcon(EditorTools.KitIcons.TARGET, "扫描", PerformScan);
            toolbar.Add(scanBtn);

            return toolbar;
        }

        /// <summary>
        /// 打开目录选择器，并将结果转换为 Unity 项目相对路径。
        /// </summary>
        private void BrowseScanFolder()
        {
            var folder = EditorUtility.OpenFolderPanel("选择扫描目录", mScanFolder, "");
            if (!string.IsNullOrEmpty(folder))
            {
                int idx = folder.IndexOf("Assets", StringComparison.Ordinal);
                mScanFolder = idx >= 0 ? folder[idx..] : folder;
                mScanFolderField.value = mScanFolder;
            }
        }

        /// <summary>
        /// 执行事件代码扫描并刷新聚合结果视图。
        /// </summary>
        private void PerformScan()
        {
            mScanResults.Clear();
            mScanResults.AddRange(EventCodeScanner.ScanFolder(mScanFolder, true, mExcludeEditor));
            RefreshScanResults();
        }

        /// <summary>
        /// 对扫描结果执行关键字过滤。
        /// </summary>
        private void PerformSearch(string keyword)
        {
            RefreshScanResults(keyword);
        }

        /// <summary>
        /// 使用当前扫描结果重建代码扫描视图。
        /// </summary>
        private void RefreshScanResults()
        {
            RefreshScanResults(mSearchKeyword);
        }

        /// <summary>
        /// 使用给定关键字过滤后重建代码扫描视图与快速导航。
        /// </summary>
        private void RefreshScanResults(string keyword)
        {
            if (mScanResultsScrollView == null)
            {
                return;
            }

            mScanResultsScrollView.Clear();

            var flows = BuildFilteredFlows(keyword);
            int totalFlowCount = 0;
            foreach (var typeGroup in flows.Values)
            {
                totalFlowCount += typeGroup.Count;
            }

            if (mScanSummaryLabel != null)
            {
                mScanSummaryLabel.text = $"事件流: {totalFlowCount} / 命中: {mScanResults.Count}";
            }

            if (totalFlowCount == 0)
            {
                mScanResultsScrollView.Add(CreateCodeScanEmptyState("未找到匹配的事件流。"));
                RefreshQuickNav(flows);
                return;
            }

            RenderEventFlowsByType(flows);
            RefreshQuickNav(flows);
        }

        /// <summary>
        /// 根据关键字将扫描结果聚合成事件流视图模型。
        /// </summary>
        private Dictionary<string, Dictionary<string, EventFlowData>> BuildFilteredFlows(string keyword)
        {
            var flows = new Dictionary<string, Dictionary<string, EventFlowData>>(3);
            string filter = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim().ToLowerInvariant();

            for (int i = 0; i < mScanResults.Count; i++)
            {
                var result = mScanResults[i];
                if (!MatchesSearch(result, filter))
                {
                    continue;
                }

                if (!flows.TryGetValue(result.EventType, out var typeMap))
                {
                    typeMap = new Dictionary<string, EventFlowData>();
                    flows[result.EventType] = typeMap;
                }

                string flowKey = $"{result.EventKey}|{result.ParamType}";
                if (!typeMap.TryGetValue(flowKey, out var flow))
                {
                    flow = new EventFlowData
                    {
                        EventType = result.EventType,
                        EventKey = result.EventKey,
                        ParamType = result.ParamType
                    };
                    typeMap[flowKey] = flow;
                }

                switch (result.CallType)
                {
                    case "Send":
                        flow.Senders.Add(result);
                        break;
                    case "Register":
                        flow.Receivers.Add(result);
                        break;
                    case "UnRegister":
                        flow.Unregisters.Add(result);
                        break;
                }
            }

            return flows;
        }

        /// <summary>
        /// 判断单条扫描结果是否匹配搜索关键字。
        /// </summary>
        private static bool MatchesSearch(EventCodeScanner.ScanResult result, string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return true;
            }

            return (result.EventType?.ToLowerInvariant().Contains(filter) ?? false) ||
                   (result.EventKey?.ToLowerInvariant().Contains(filter) ?? false) ||
                   (result.ParamType?.ToLowerInvariant().Contains(filter) ?? false) ||
                   (result.FilePath?.ToLowerInvariant().Contains(filter) ?? false) ||
                   (result.LineContent?.ToLowerInvariant().Contains(filter) ?? false);
        }

        /// <summary>
        /// 创建空状态提示。
        /// </summary>
        private static VisualElement CreateCodeScanEmptyState(string message)
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            container.style.justifyContent = Justify.Center;
            container.style.alignItems = Align.Center;
            container.style.minHeight = 120;

            var label = new Label(message);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.65f));
            container.Add(label);

            return container;
        }

        /// <summary>
        /// 切换搜索框占位文本样式。
        /// </summary>
        private static void SetTextFieldPlaceholderStyle(TextField field, bool isPlaceholder)
        {
            field.style.color = new StyleColor(isPlaceholder
                ? new Color(0.45f, 0.45f, 0.5f)
                : new Color(0.85f, 0.85f, 0.88f));
        }

        #endregion
    }
}
#endif
