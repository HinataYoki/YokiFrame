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
    /// EventKit 旧版查看窗口。
    /// 包含运行时监控、事件历史和代码扫描三个视图。
    /// </summary>
    public partial class EventKitViewerWindow : YokiMonitorWindowBase
    {
        private const string WINDOW_TITLE = "EventKit Viewer";
        private const float REFRESH_INTERVAL = 0.5f;

        private enum ViewMode { Runtime, History, CodeScan }
        private enum EventCategory { Enum, Type, String }

        private ViewMode mViewMode = ViewMode.Runtime;
        private EventCategory mSelectedCategory = EventCategory.Enum;
        private string mSelectedEventKey;

        private readonly List<EventNodeData> mCachedNodes = new(32);
        private readonly List<ListenerDisplayData> mCachedListeners = new(16);
        private string mScanFolder = "Assets/Scripts";
        private readonly List<EventCodeScanner.ScanResult> mScanResults = new(64);
        private string mScanFilterType = "All";
        private string mScanFilterCall = "All";
        private string mHistoryFilterAction = "All";
        private string mHistoryFilterType = "All";
        private bool mHistoryAutoScroll = true;
        private bool mClearHistoryOnStop = true;

        private YokiFrameUIComponents.TabView mTabView;
        private VisualElement mRuntimeView;
        private VisualElement mHistoryView;
        private VisualElement mCodeScanView;

        private Button mEnumCategoryBtn;
        private Button mTypeCategoryBtn;
        private Button mStringCategoryBtn;
        private ListView mEventListView;
        private VisualElement mListenerDetailContainer;
        private Label mEventCountLabel;

        private ListView mHistoryListView;
        private Label mHistoryCountLabel;
        private DropdownField mHistoryActionFilter;
        private DropdownField mHistoryTypeFilter;
        private Toggle mRecordSendToggle;
        private Toggle mRecordStackToggle;
        private Toggle mAutoScrollToggle;
        private Toggle mClearOnStopToggle;

        private TextField mScanFolderField;
        private DropdownField mScanTypeFilter;
        private DropdownField mScanCallFilter;
        private ListView mScanResultsListView;
        private Label mScanCountLabel;

        protected override float RefreshIntervalSeconds => REFRESH_INTERVAL;
        protected override string MonitorKitName => "EventKit";

        /// <summary>
        /// 打开旧版 EventKit 查看窗口。
        /// </summary>
        public static void Open()
        {
            OpenMonitorWindow<EventKitViewerWindow>(WINDOW_TITLE, new Vector2(900, 500));
        }

        private void OnEnable()
        {
            HandleMonitorWindowEnable();
        }

        private void OnDisable()
        {
            HandleMonitorWindowDisable();
        }

        /// <summary>
        /// 读取窗口级偏好设置。
        /// </summary>
        protected override void OnMonitorEnabled()
        {
            mScanFolder = EditorPrefs.GetString("EventKitViewer_ScanFolder", "Assets/Scripts");
            mClearHistoryOnStop = EditorPrefs.GetBool("EventKitViewer_ClearHistoryOnStop", true);
        }

        /// <summary>
        /// 持久化窗口级偏好设置。
        /// </summary>
        protected override void OnMonitorDisabled()
        {
            EditorPrefs.SetString("EventKitViewer_ScanFolder", mScanFolder);
            EditorPrefs.SetBool("EventKitViewer_ClearHistoryOnStop", mClearHistoryOnStop);
        }

        /// <summary>
        /// 切换 PlayMode 时重置运行时缓存。
        /// </summary>
        protected override void OnMonitorPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                mCachedNodes.Clear();
                mCachedListeners.Clear();
                mSelectedEventKey = null;
                RefreshRuntimeView();
            }
        }

        private void Update()
        {
            HandleMonitorWindowUpdate();
        }

        /// <summary>
        /// 定时刷新监控数据。
        /// </summary>
        protected override void RefreshMonitorData() => RefreshEventData();

        private void CreateGUI()
        {
            InitializeMonitorWindowUI(rootVisualElement);
        }

        /// <summary>
        /// 构建窗口主界面。
        /// </summary>
        protected override void BuildMonitorUI(VisualElement root)
        {
            BuildRuntimeView();
            BuildHistoryView();
            BuildCodeScanView();

            mTabView = YokiFrameUIComponents.CreateTabView(
                ("运行时监控", mRuntimeView),
                ("事件历史", mHistoryView),
                ("代码扫描", mCodeScanView)
            );
            mTabView.OnTabChanged += OnTabChanged;
            root.Add(mTabView.Root);
        }

        /// <summary>
        /// 切换窗口顶部标签。
        /// </summary>
        private void OnTabChanged(int index)
        {
            mViewMode = index switch
            {
                0 => ViewMode.Runtime,
                1 => ViewMode.History,
                2 => ViewMode.CodeScan,
                _ => ViewMode.Runtime
            };

            if (mViewMode == ViewMode.Runtime && EditorApplication.isPlaying)
            {
                RefreshEventData();
            }
            else if (mViewMode == ViewMode.History)
            {
                RefreshHistoryView();
            }
        }

        /// <summary>
        /// 创建事件类别切换按钮。
        /// </summary>
        private Button CreateCategoryButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.height = 22;
            btn.style.marginRight = 4;
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            return btn;
        }

        /// <summary>
        /// 刷新类别按钮高亮状态。
        /// </summary>
        private void UpdateCategoryButtonState(Button btn, bool isActive)
        {
            btn.style.backgroundColor = new StyleColor(isActive
                ? new Color(0.25f, 0.45f, 0.65f)
                : new Color(0.25f, 0.25f, 0.25f));
            btn.style.color = new StyleColor(isActive ? Color.white : new Color(0.7f, 0.7f, 0.7f));
        }

        /// <summary>
        /// 打开脚本并跳转到指定行号。
        /// </summary>
        private static void OpenFileAtLine(string filePath, int line)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset, line);
            }
        }

        /// <summary>
        /// 获取事件历史动作对应颜色。
        /// </summary>
        private static Color GetActionColor(string action) => action switch
        {
            "Register" => new Color(0.4f, 0.8f, 0.4f),
            "UnRegister" => new Color(0.9f, 0.4f, 0.4f),
            "Send" => new Color(0.4f, 0.7f, 0.9f),
            _ => new Color(0.6f, 0.6f, 0.6f)
        };

        /// <summary>
        /// 获取事件类型对应颜色。
        /// </summary>
        private static Color GetEventTypeColor(string eventType) => eventType switch
        {
            "Enum" => new Color(0.4f, 0.7f, 1f),
            "Type" => new Color(0.5f, 0.9f, 0.5f),
            "String" => new Color(1f, 0.8f, 0.4f),
            _ => new Color(0.7f, 0.7f, 0.7f)
        };

        #region 运行时视图构建

        /// <summary>
        /// 构建运行时监控视图。
        /// </summary>
        private void BuildRuntimeView()
        {
            mRuntimeView = new VisualElement();
            mRuntimeView.style.flexGrow = 1;
            mRuntimeView.style.flexDirection = FlexDirection.Column;

            BuildRuntimeToolbar();
            BuildRuntimeContent();
        }

        /// <summary>
        /// 构建运行时视图工具栏。
        /// </summary>
        private void BuildRuntimeToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 28;
            toolbar.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.alignItems = Align.Center;
            mRuntimeView.Add(toolbar);

            var label = new Label("事件类型:");
            label.style.marginRight = 8;
            label.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            toolbar.Add(label);

            mEnumCategoryBtn = CreateCategoryButton("Enum", () => SwitchCategory(EventCategory.Enum));
            mTypeCategoryBtn = CreateCategoryButton("Type", () => SwitchCategory(EventCategory.Type));
            mStringCategoryBtn = CreateCategoryButton("String", () => SwitchCategory(EventCategory.String));

            toolbar.Add(mEnumCategoryBtn);
            toolbar.Add(mTypeCategoryBtn);
            toolbar.Add(mStringCategoryBtn);

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            var refreshButton = new Button(RefreshEventData) { text = "刷新" };
            refreshButton.style.height = 22;
            refreshButton.style.paddingLeft = 12;
            refreshButton.style.paddingRight = 12;
            toolbar.Add(refreshButton);

            UpdateCategoryButtons();
        }

        /// <summary>
        /// 构建运行时主内容区域。
        /// </summary>
        private void BuildRuntimeContent()
        {
            var content = new VisualElement();
            content.style.flexGrow = 1;
            content.style.flexDirection = FlexDirection.Row;
            content.style.paddingTop = 8;
            content.style.paddingBottom = 8;
            content.style.paddingLeft = 8;
            content.style.paddingRight = 8;
            mRuntimeView.Add(content);

            BuildEventListPanel(content);
            BuildListenerDetailPanel(content);
        }

        /// <summary>
        /// 构建事件列表面板。
        /// </summary>
        private void BuildEventListPanel(VisualElement parent)
        {
            var panel = new VisualElement();
            panel.style.width = 280;
            panel.style.marginRight = 8;
            panel.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            panel.style.borderTopLeftRadius = 4;
            panel.style.borderTopRightRadius = 4;
            panel.style.borderBottomLeftRadius = 4;
            panel.style.borderBottomRightRadius = 4;
            parent.Add(panel);

            mEventCountLabel = new Label("(0)");
            mEventCountLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            panel.Add(CreateMonitorPanelHeader("已注册事件", KitIcons.EVENTKIT, mEventCountLabel));

            mEventListView = new ListView();
            mEventListView.fixedItemHeight = 28;
            mEventListView.makeItem = MakeEventItem;
            mEventListView.bindItem = BindEventItem;
#if UNITY_2022_1_OR_NEWER
            mEventListView.selectionChanged += OnEventSelected;
#else
            mEventListView.onSelectionChange += OnEventSelected;
#endif
            mEventListView.style.flexGrow = 1;
            panel.Add(mEventListView);
        }

        /// <summary>
        /// 构建监听器详情面板。
        /// </summary>
        private void BuildListenerDetailPanel(VisualElement parent)
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;
            panel.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            panel.style.borderTopLeftRadius = 4;
            panel.style.borderTopRightRadius = 4;
            panel.style.borderBottomLeftRadius = 4;
            panel.style.borderBottomRightRadius = 4;
            parent.Add(panel);

            panel.Add(CreateMonitorPanelHeader("监听器详情", KitIcons.RECEIVE));

            mListenerDetailContainer = new ScrollView(ScrollViewMode.Vertical);
            mListenerDetailContainer.style.flexGrow = 1;
            mListenerDetailContainer.style.paddingTop = 8;
            mListenerDetailContainer.style.paddingBottom = 8;
            mListenerDetailContainer.style.paddingLeft = 12;
            mListenerDetailContainer.style.paddingRight = 12;
            panel.Add(mListenerDetailContainer);

            var hint = new Label("选择左侧事件查看监听器详情");
            hint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginTop = 50;
            mListenerDetailContainer.Add(hint);
        }

        #endregion

        #region 类别切换

        /// <summary>
        /// 切换当前事件类别。
        /// </summary>
        private void SwitchCategory(EventCategory category)
        {
            mSelectedCategory = category;
            mSelectedEventKey = null;
            UpdateCategoryButtons();
            RefreshEventData();
        }

        /// <summary>
        /// 更新类别按钮激活状态。
        /// </summary>
        private void UpdateCategoryButtons()
        {
            if (mEnumCategoryBtn == null)
            {
                return;
            }

            UpdateCategoryButtonState(mEnumCategoryBtn, mSelectedCategory == EventCategory.Enum);
            UpdateCategoryButtonState(mTypeCategoryBtn, mSelectedCategory == EventCategory.Type);
            UpdateCategoryButtonState(mStringCategoryBtn, mSelectedCategory == EventCategory.String);
        }

        #endregion
    }
}
#endif
