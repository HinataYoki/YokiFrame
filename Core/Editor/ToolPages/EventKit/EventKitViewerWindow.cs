#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 事件可视化查看器
    /// 使用 UI Toolkit 实现，支持 Unity 2021.3+
    /// </summary>
    public partial class EventKitViewerWindow : EditorWindow
    {
        #region 常量

        private const string WINDOW_TITLE = "EventKit Viewer";
        private const float REFRESH_INTERVAL = 0.5f;

        #endregion

        #region 枚举

        private enum ViewMode { Runtime, History, CodeScan }
        private enum EventCategory { Enum, Type, String }

        #endregion

        #region 状态字段

        private ViewMode mViewMode = ViewMode.Runtime;
        private EventCategory mSelectedCategory = EventCategory.Enum;
        private string mSelectedEventKey;
        private double mLastRefreshTime;

        #endregion

        #region 数据缓存

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

        #endregion

        #region UI 元素引用

        private VisualElement mContentContainer;
        private VisualElement mRuntimeView;
        private VisualElement mHistoryView;
        private VisualElement mCodeScanView;
        private Button mRuntimeTabBtn;
        private Button mHistoryTabBtn;
        private Button mCodeScanTabBtn;

        // 运行时视图
        private Button mEnumCategoryBtn;
        private Button mTypeCategoryBtn;
        private Button mStringCategoryBtn;
        private ListView mEventListView;
        private VisualElement mListenerDetailContainer;
        private Label mEventCountLabel;

        // 历史视图
        private ListView mHistoryListView;
        private Label mHistoryCountLabel;
        private DropdownField mHistoryActionFilter;
        private DropdownField mHistoryTypeFilter;
        private Toggle mRecordSendToggle;
        private Toggle mRecordStackToggle;
        private Toggle mAutoScrollToggle;
        private Toggle mClearOnStopToggle;

        // 代码扫描视图
        private TextField mScanFolderField;
        private DropdownField mScanTypeFilter;
        private DropdownField mScanCallFilter;
        private ListView mScanResultsListView;
        private Label mScanCountLabel;

        #endregion

        #region 窗口入口

        public static void Open()
        {
            var window = GetWindow<EventKitViewerWindow>(false, WINDOW_TITLE);
            window.minSize = new Vector2(900, 500);
            window.Show();
        }

        #endregion

        #region 生命周期

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
                RefreshRuntimeView();
            }
        }

        private void Update()
        {
            if (!EditorApplication.isPlaying) return;

            if (EditorApplication.timeSinceStartup - mLastRefreshTime > REFRESH_INTERVAL)
            {
                RefreshEventData();
                mLastRefreshTime = EditorApplication.timeSinceStartup;
            }
        }

        #endregion

        #region UI Toolkit 入口

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));

            BuildToolbar(root);
            BuildContentArea(root);

            // 默认显示运行时视图
            SwitchView(ViewMode.Runtime);
        }

        /// <summary>
        /// 构建顶部工具栏
        /// </summary>
        private void BuildToolbar(VisualElement parent)
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 28;
            toolbar.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f));
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
            parent.Add(toolbar);

            // 视图切换按钮
            mRuntimeTabBtn = CreateTabButton("运行时监控", () => SwitchView(ViewMode.Runtime));
            mHistoryTabBtn = CreateTabButton("事件历史", () => SwitchView(ViewMode.History));
            mCodeScanTabBtn = CreateTabButton("代码扫描", () => SwitchView(ViewMode.CodeScan));

            toolbar.Add(mRuntimeTabBtn);
            toolbar.Add(mHistoryTabBtn);
            toolbar.Add(mCodeScanTabBtn);
        }

        /// <summary>
        /// 构建内容区域
        /// </summary>
        private void BuildContentArea(VisualElement parent)
        {
            mContentContainer = new VisualElement();
            mContentContainer.style.flexGrow = 1;
            parent.Add(mContentContainer);

            // 构建三个视图
            BuildRuntimeView();
            BuildHistoryView();
            BuildCodeScanView();
        }

        /// <summary>
        /// 切换视图
        /// </summary>
        private void SwitchView(ViewMode mode)
        {
            mViewMode = mode;

            // 更新标签按钮状态
            UpdateTabButtonState(mRuntimeTabBtn, mode == ViewMode.Runtime);
            UpdateTabButtonState(mHistoryTabBtn, mode == ViewMode.History);
            UpdateTabButtonState(mCodeScanTabBtn, mode == ViewMode.CodeScan);

            // 显示对应视图
            mRuntimeView.style.display = mode == ViewMode.Runtime ? DisplayStyle.Flex : DisplayStyle.None;
            mHistoryView.style.display = mode == ViewMode.History ? DisplayStyle.Flex : DisplayStyle.None;
            mCodeScanView.style.display = mode == ViewMode.CodeScan ? DisplayStyle.Flex : DisplayStyle.None;

            // 刷新数据
            if (mode == ViewMode.Runtime && EditorApplication.isPlaying)
                RefreshEventData();
            else if (mode == ViewMode.History)
                RefreshHistoryView();
        }

        #endregion

        #region UI 辅助方法

        /// <summary>
        /// 创建标签按钮
        /// </summary>
        private Button CreateTabButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.height = 24;
            btn.style.marginRight = 4;
            btn.style.paddingLeft = 12;
            btn.style.paddingRight = 12;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 0;
            btn.style.borderBottomRightRadius = 0;
            btn.style.borderBottomWidth = 0;
            return btn;
        }

        /// <summary>
        /// 更新标签按钮状态
        /// </summary>
        private void UpdateTabButtonState(Button btn, bool isActive)
        {
            btn.style.backgroundColor = new StyleColor(isActive
                ? new Color(0.3f, 0.5f, 0.7f)
                : new Color(0.25f, 0.25f, 0.25f));
            btn.style.color = new StyleColor(isActive ? Color.white : new Color(0.7f, 0.7f, 0.7f));
        }

        /// <summary>
        /// 创建区块容器
        /// </summary>
        private VisualElement CreateSection(string title = null)
        {
            var section = new VisualElement();
            section.style.marginBottom = 8;
            section.style.paddingTop = 8;
            section.style.paddingBottom = 8;
            section.style.paddingLeft = 12;
            section.style.paddingRight = 12;
            section.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            section.style.borderTopLeftRadius = 4;
            section.style.borderTopRightRadius = 4;
            section.style.borderBottomLeftRadius = 4;
            section.style.borderBottomRightRadius = 4;

            if (!string.IsNullOrEmpty(title))
            {
                var titleLabel = new Label(title);
                titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                titleLabel.style.marginBottom = 8;
                titleLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
                section.Add(titleLabel);
            }

            return section;
        }

        /// <summary>
        /// 创建类别按钮
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
        /// 更新类别按钮状态
        /// </summary>
        private void UpdateCategoryButtonState(Button btn, bool isActive)
        {
            btn.style.backgroundColor = new StyleColor(isActive
                ? new Color(0.25f, 0.45f, 0.65f)
                : new Color(0.25f, 0.25f, 0.25f));
            btn.style.color = new StyleColor(isActive ? Color.white : new Color(0.7f, 0.7f, 0.7f));
        }

        /// <summary>
        /// 打开文件并跳转到指定行
        /// </summary>
        private static void OpenFileAtLine(string filePath, int line)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
            if (asset != null)
                AssetDatabase.OpenAsset(asset, line);
        }

        /// <summary>
        /// 获取操作类型颜色
        /// </summary>
        private static Color GetActionColor(string action) => action switch
        {
            "Register" => new Color(0.4f, 0.8f, 0.4f),
            "UnRegister" => new Color(0.9f, 0.4f, 0.4f),
            "Send" => new Color(0.4f, 0.7f, 0.9f),
            _ => new Color(0.6f, 0.6f, 0.6f)
        };

        /// <summary>
        /// 获取事件类型颜色
        /// </summary>
        private static Color GetEventTypeColor(string eventType) => eventType switch
        {
            "Enum" => new Color(0.4f, 0.7f, 1f),
            "Type" => new Color(0.5f, 0.9f, 0.5f),
            "String" => new Color(1f, 0.8f, 0.4f),
            _ => new Color(0.7f, 0.7f, 0.7f)
        };

        #endregion

        #region 数据结构

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

        #endregion
    }
}
#endif
