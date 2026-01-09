#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// FsmKit 状态机可视化查看器
    /// 使用 UI Toolkit 实现，支持 Unity 2021.3+
    /// </summary>
    public partial class FsmKitViewerWindow : EditorWindow
    {
        #region 常量

        private const string WINDOW_TITLE = "FsmKit Viewer";
        private const float REFRESH_INTERVAL = 0.2f;

        #endregion

        #region 枚举

        private enum ViewMode { Runtime, History }

        #endregion

        #region 状态字段

        private ViewMode mViewMode = ViewMode.Runtime;
        private double mLastRefreshTime;
        private bool mClearHistoryOnStop = true;
        private bool mHistoryAutoScroll = true;
        private string mHistoryFilterAction = "All";

        #endregion

        #region 数据缓存

        private readonly List<IFSM> mCachedFsms = new(16);
        private IFSM mSelectedFsm;

        #endregion

        #region UI 元素引用

        private VisualElement mContentContainer;
        private VisualElement mRuntimeView;
        private VisualElement mHistoryView;
        private Button mRuntimeTabBtn;
        private Button mHistoryTabBtn;

        // 运行时视图
        private ListView mFsmListView;
        private VisualElement mFsmDetailContainer;
        private Label mFsmCountLabel;

        // 历史视图
        private ListView mHistoryListView;
        private Label mHistoryCountLabel;
        private DropdownField mHistoryActionFilter;
        private Toggle mRecordTransitionsToggle;
        private Toggle mAutoScrollToggle;
        private Toggle mClearOnStopToggle;

        #endregion

        #region 窗口入口

        public static void Open()
        {
            var window = GetWindow<FsmKitViewerWindow>(false, WINDOW_TITLE);
            window.minSize = new Vector2(800, 450);
            window.Show();
        }

        #endregion

        #region 生命周期

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            mClearHistoryOnStop = EditorPrefs.GetBool("FsmKitViewer_ClearHistoryOnStop", true);
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorPrefs.SetBool("FsmKitViewer_ClearHistoryOnStop", mClearHistoryOnStop);
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                mCachedFsms.Clear();
                mSelectedFsm = null;
            }
            RefreshRuntimeView();
        }

        private void Update()
        {
            if (!EditorApplication.isPlaying) return;

            if (EditorApplication.timeSinceStartup - mLastRefreshTime > REFRESH_INTERVAL)
            {
                FsmDebugger.GetActiveFsms(mCachedFsms);
                RefreshFsmList();
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

            mRuntimeTabBtn = CreateTabButton("运行时监控", () => SwitchView(ViewMode.Runtime));
            mHistoryTabBtn = CreateTabButton("转换历史", () => SwitchView(ViewMode.History));

            toolbar.Add(mRuntimeTabBtn);
            toolbar.Add(mHistoryTabBtn);
        }

        /// <summary>
        /// 构建内容区域
        /// </summary>
        private void BuildContentArea(VisualElement parent)
        {
            mContentContainer = new VisualElement();
            mContentContainer.style.flexGrow = 1;
            parent.Add(mContentContainer);

            BuildRuntimeView();
            BuildHistoryView();
        }

        /// <summary>
        /// 切换视图
        /// </summary>
        private void SwitchView(ViewMode mode)
        {
            mViewMode = mode;

            UpdateTabButtonState(mRuntimeTabBtn, mode == ViewMode.Runtime);
            UpdateTabButtonState(mHistoryTabBtn, mode == ViewMode.History);

            mRuntimeView.style.display = mode == ViewMode.Runtime ? DisplayStyle.Flex : DisplayStyle.None;
            mHistoryView.style.display = mode == ViewMode.History ? DisplayStyle.Flex : DisplayStyle.None;

            if (mode == ViewMode.Runtime && EditorApplication.isPlaying)
            {
                FsmDebugger.GetActiveFsms(mCachedFsms);
                RefreshFsmList();
            }
            else if (mode == ViewMode.History)
            {
                RefreshHistoryView();
            }
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

        #endregion
    }
}
#endif
