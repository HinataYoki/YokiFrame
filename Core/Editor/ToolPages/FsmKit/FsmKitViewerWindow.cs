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

        private YokiFrameUIComponents.TabView mTabView;
        private VisualElement mRuntimeView;
        private VisualElement mHistoryView;

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

            // 加载样式表
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/YokiFrame/Core/Editor/Styles/YokiFrameToolStyles.uss");
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            // 构建两个视图
            BuildRuntimeView();
            BuildHistoryView();

            // 使用统一的标签页视图组件
            mTabView = YokiFrameUIComponents.CreateTabView(
                ("运行时监控", mRuntimeView),
                ("转换历史", mHistoryView)
            );
            mTabView.OnTabChanged += OnTabChanged;
            root.Add(mTabView.Root);
        }

        /// <summary>
        /// 标签页切换回调
        /// </summary>
        private void OnTabChanged(int index)
        {
            mViewMode = index == 0 ? ViewMode.Runtime : ViewMode.History;

            if (mViewMode == ViewMode.Runtime && EditorApplication.isPlaying)
            {
                FsmDebugger.GetActiveFsms(mCachedFsms);
                RefreshFsmList();
            }
            else if (mViewMode == ViewMode.History)
            {
                RefreshHistoryView();
            }
        }

        #endregion
    }
}
#endif
