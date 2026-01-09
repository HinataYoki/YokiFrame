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
    /// EventKit 工具页面 - 静态代码扫描视图
    /// 设计目标：可视化数据流向与逻辑健康度
    /// 布局模式：三栏流式布局（发送 -> 事件 -> 接收）
    /// </summary>
    public partial class EventKitToolPage
    {
        #region 健康状态枚举

        private enum HealthStatus
        {
            Healthy,    // 完美闭环：有发有收
            Orphan,     // 孤儿事件：有发送者，无接收者
            LeakRisk,   // 潜在泄露：Register > Unregister
            NoSender    // 无发送源：有接收者，无发送者
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
        /// 事件流数据
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
                var hasSenders = Senders.Count > 0;
                var hasReceivers = Receivers.Count > 0;

                if (!hasSenders && hasReceivers)
                    return HealthStatus.NoSender;
                if (hasSenders && !hasReceivers)
                    return HealthStatus.Orphan;
                if (Receivers.Count > Unregisters.Count && Unregisters.Count > 0)
                    return HealthStatus.LeakRisk;
                return HealthStatus.Healthy;
            }
        }

        #endregion

        #region 创建代码扫描视图

        private VisualElement CreateCodeScanView()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            // 关键：设置 overflow 为 hidden，确保子元素不会撑开容器
            container.style.overflow = Overflow.Hidden;

            // 工具栏
            var toolbar = CreateScanToolbar();
            container.Add(toolbar);

            // 主内容区域：水平布局（左侧滚动视图 + 右侧快速导航）
            var mainContent = new VisualElement();
            mainContent.style.flexDirection = FlexDirection.Row;
            mainContent.style.flexGrow = 1;
            mainContent.style.flexShrink = 1;
            mainContent.style.overflow = Overflow.Hidden;
            // 注册响应式布局回调
            mainContent.RegisterCallback<GeometryChangedEvent>(OnCodeScanViewGeometryChanged);
            container.Add(mainContent);

            // 结果滚动视图（约 66%，2:1 比例）
            mScanResultsScrollView = new ScrollView(ScrollViewMode.Vertical);
            mScanResultsScrollView.style.flexGrow = 2;
            mScanResultsScrollView.style.flexBasis = 0;
            mScanResultsScrollView.style.flexShrink = 1;
            mScanResultsScrollView.style.paddingLeft = 16;
            mScanResultsScrollView.style.paddingRight = 16;
            mScanResultsScrollView.style.paddingTop = 16;
            // 始终显示垂直滚动条
            mScanResultsScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            mainContent.Add(mScanResultsScrollView);

            // 右侧快速导航面板
            var quickNavPanel = CreateQuickNavPanel();
            mainContent.Add(quickNavPanel);

            return container;
        }

        private VisualElement CreateScanToolbar()
        {
            var toolbar = YokiFrameUIComponents.CreateToolbar();

            var folderLabel = new Label("扫描目录:");
            folderLabel.AddToClassList("toolbar-label");
            toolbar.Add(folderLabel);

            mScanFolderField = new TextField();
            mScanFolderField.value = mScanFolder;
            mScanFolderField.style.width = 200;
            mScanFolderField.RegisterValueChangedCallback(evt => mScanFolder = evt.newValue);
            toolbar.Add(mScanFolderField);

            var browseBtn = YokiFrameUIComponents.CreateToolbarButton("...", BrowseScanFolder);
            toolbar.Add(browseBtn);

            // 排除 Editor 开关
            var excludeEditorToggle = YokiFrameUIComponents.CreateModernToggle("排除Editor", mExcludeEditor, 
                value => mExcludeEditor = value);
            excludeEditorToggle.style.marginLeft = 8;
            toolbar.Add(excludeEditorToggle);

            // 搜索框
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
            var placeholder = "搜索事件...";
            if (string.IsNullOrEmpty(mSearchField.value))
            {
                mSearchField.Q<TextElement>().style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                mSearchField.SetValueWithoutNotify(placeholder);
            }
            mSearchField.RegisterCallback<FocusInEvent>(_ =>
            {
                if (mSearchField.value == placeholder)
                {
                    mSearchField.SetValueWithoutNotify("");
                    mSearchField.Q<TextElement>().style.color = StyleKeyword.Null;
                }
            });
            mSearchField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (string.IsNullOrEmpty(mSearchField.value))
                {
                    mSearchField.Q<TextElement>().style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
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

            toolbar.Add(YokiFrameUIComponents.CreateFlexSpacer());

            mScanSummaryLabel = new Label();
            mScanSummaryLabel.AddToClassList("toolbar-label");
            toolbar.Add(mScanSummaryLabel);

            var scanBtn = YokiFrameUIComponents.CreateToolbarButtonWithIcon(EditorTools.KitIcons.TARGET, "扫描", PerformScan);
            toolbar.Add(scanBtn);

            return toolbar;
        }

        private void BrowseScanFolder()
        {
            var folder = EditorUtility.OpenFolderPanel("选择扫描目录", mScanFolder, "");
            if (!string.IsNullOrEmpty(folder))
            {
                var idx = folder.IndexOf("Assets", StringComparison.Ordinal);
                mScanFolder = idx >= 0 ? folder[idx..] : folder;
                mScanFolderField.value = mScanFolder;
            }
        }

        private void PerformScan()
        {
            mScanResults.Clear();
            mScanResults.AddRange(EventCodeScanner.ScanFolder(mScanFolder, true, mExcludeEditor));
            RefreshScanResults();
        }

        /// <summary>
        /// 执行搜索，高亮匹配的事件并滚动到第一个匹配项
        /// </summary>
        private void PerformSearch(string keyword)
        {
            // 清除之前的高亮
            ClearSearchHighlight();

            if (string.IsNullOrWhiteSpace(keyword)) return;

            keyword = keyword.ToLowerInvariant();
            VisualElement firstMatch = null;

            // 遍历导航映射，查找匹配项
            foreach (var (navItem, targetElement) in mNavItemMap)
            {
                var eventKey = navItem.userData as string;
                if (eventKey != null && eventKey.ToLowerInvariant().Contains(keyword))
                {
                    // 高亮目标元素
                    HighlightElement(targetElement);
                    
                    // 高亮导航项
                    HighlightNavItem(navItem);

                    if (firstMatch == null)
                        firstMatch = targetElement;
                }
            }

            // 滚动到第一个匹配项
            if (firstMatch != null)
            {
                mScanResultsScrollView.ScrollTo(firstMatch);
            }
        }

        /// <summary>
        /// 高亮元素
        /// </summary>
        private void HighlightElement(VisualElement element)
        {
            element.style.borderLeftWidth = 3;
            element.style.borderLeftColor = new StyleColor(new Color(0.34f, 0.61f, 0.84f));
            element.style.backgroundColor = new StyleColor(new Color(0.24f, 0.37f, 0.58f, 0.2f));
            mHighlightedElements.Add(element);
        }

        /// <summary>
        /// 高亮导航项
        /// </summary>
        private void HighlightNavItem(VisualElement navItem)
        {
            navItem.style.backgroundColor = new StyleColor(new Color(0.34f, 0.61f, 0.84f, 0.3f));
            if (!mHighlightedElements.Contains(navItem))
                mHighlightedElements.Add(navItem);
        }

        /// <summary>
        /// 清除搜索高亮
        /// </summary>
        private void ClearSearchHighlight()
        {
            foreach (var element in mHighlightedElements)
            {
                // 重置内容元素样式
                element.style.borderLeftWidth = StyleKeyword.Null;
                element.style.borderLeftColor = StyleKeyword.Null;
                element.style.backgroundColor = StyleKeyword.Null;
            }
            mHighlightedElements.Clear();
        }

        #endregion

        #region 刷新扫描结果

        private void RefreshScanResults()
        {
            mScanResultsScrollView.Clear();

            if (mScanResults.Count == 0)
            {
                mScanSummaryLabel.text = "无结果";
                mScanResultsScrollView.Add(CreateEmptyState("点击「扫描」按钮开始扫描代码"));
                // 清空快速导航
                RefreshQuickNav(new Dictionary<string, Dictionary<string, EventFlowData>>());
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

            // 构建事件流数据
            var eventFlows = BuildEventFlows();

            // 先刷新快速导航（需要在渲染前创建导航项）
            RefreshQuickNav(eventFlows);

            // 按类型分组渲染
            RenderEventFlowsByType(eventFlows);
        }

        /// <summary>
        /// 构建事件流数据结构
        /// </summary>
        private Dictionary<string, Dictionary<string, EventFlowData>> BuildEventFlows()
        {
            var flows = new Dictionary<string, Dictionary<string, EventFlowData>>();

            foreach (var result in mScanResults)
            {
                var eventType = result.EventType;
                var eventKey = result.EventKey;

                if (!flows.TryGetValue(eventType, out var typeDict))
                {
                    typeDict = new Dictionary<string, EventFlowData>();
                    flows[eventType] = typeDict;
                }

                if (!typeDict.TryGetValue(eventKey, out var flowData))
                {
                    flowData = new EventFlowData
                    {
                        EventType = eventType,
                        EventKey = eventKey,
                        ParamType = result.ParamType
                    };
                    typeDict[eventKey] = flowData;
                }

                switch (result.CallType)
                {
                    case "Send":
                        flowData.Senders.Add(result);
                        break;
                    case "Register":
                        flowData.Receivers.Add(result);
                        break;
                    case "UnRegister":
                        flowData.Unregisters.Add(result);
                        break;
                }
            }

            return flows;
        }

        #endregion
    }
}
#endif
