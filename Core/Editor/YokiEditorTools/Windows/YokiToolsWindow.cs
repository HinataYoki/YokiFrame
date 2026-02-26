#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 工具总面板
    /// 
    /// 按功能拆分为多个 partial class 文件：
    /// - YokiToolsWindow.cs          ← 核心生命周期（你在这里）
    /// - YokiToolsWindow.Sidebar.cs  ← 侧边栏构建
    /// - YokiToolsWindow.Content.cs  ← 内容区域与页面切换
    /// 
    /// 依赖方向: Menu → Window → Registry → Page
    /// </summary>
    public partial class YokiToolsWindow : EditorWindow
    {
        #region 常量

        private const string WINDOW_TITLE = "YokiFrame Tools";
        private const string ICON_PATH = "yoki";
        private const string PREFS_SELECTED_PAGE = "YokiTools_SelectedPage";

        #endregion

        #region 静态字段

        private static Texture2D sIconTexture;

        #endregion

        #region 实例字段

        private readonly List<YokiPageInfo> mPageInfos = new();
        private readonly Dictionary<YokiPageInfo, IYokiToolPage> mPageInstances = new();
        private readonly Dictionary<YokiPageInfo, VisualElement> mPageElements = new();
        private readonly Dictionary<YokiPageInfo, VisualElement> mSidebarItems = new();

        private int mSelectedPageIndex;
        private VisualElement mContentContainer;
        private IYokiToolPage mActivePage;
        private YokiPageInfo? mActivePageInfo;  // 优化：O(1) 定位当前页面
        private VisualElement mSidebarHighlight;
        private VisualElement mSidebarListContainer;

        #endregion

        #region 窗口入口

        /// <summary>
        /// 打开工具面板
        /// </summary>
        public static void Open()
        {
            var window = GetWindow<YokiToolsWindow>(false, WINDOW_TITLE);
            window.minSize = new(1000, 600);
            window.titleContent = new(WINDOW_TITLE, LoadIcon());
            window.Show();
        }

        /// <summary>
        /// 打开窗口并选择指定页面
        /// </summary>
        public static void OpenAndSelectPage<T>(System.Action<T> onPageSelected = null)
            where T : class, IYokiToolPage
        {
            var window = GetWindow<YokiToolsWindow>(false, WINDOW_TITLE);
            window.minSize = new(1000, 600);
            window.titleContent = new(WINDOW_TITLE, LoadIcon());
            window.Show();
            window.Focus();

            EditorApplication.delayCall += () =>
            {
                for (int i = 0; i < window.mPageInfos.Count; i++)
                {
                    var page = window.GetOrCreatePage(window.mPageInfos[i]);
                    if (page is T targetPage)
                    {
                        window.SelectPage(i);
                        onPageSelected?.Invoke(targetPage);
                        return;
                    }
                }
            };
        }

        private static Texture2D LoadIcon()
        {
            if (sIconTexture == default)
            {
                sIconTexture = Resources.Load<Texture2D>(ICON_PATH);
            }
            return sIconTexture;
        }

        #endregion

        #region 生命周期

        private void OnEnable()
        {
            CollectPages();
            mSelectedPageIndex = EditorPrefs.GetInt(PREFS_SELECTED_PAGE, 0);
            if (mSelectedPageIndex >= mPageInfos.Count)
                mSelectedPageIndex = 0;

            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorPrefs.SetInt(PREFS_SELECTED_PAGE, mSelectedPageIndex);

            mActivePage?.OnDeactivate();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state is PlayModeStateChange.EnteredPlayMode or PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += () =>
                {
                    if (this == default || mContentContainer == default) return;

                    mPageElements.Clear();

                    if (mActivePage != default && mSelectedPageIndex >= 0 && mSelectedPageIndex < mPageInfos.Count)
                    {
                        mActivePage.OnDeactivate();
                        mActivePage = default;
                        SelectPage(mSelectedPageIndex);
                    }
                };
            }
        }

        private void OnEditorUpdate()
        {
            mActivePage?.OnUpdate();
        }

        #endregion

        #region UI 入口

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexGrow = 1;

            // ESC 键关闭窗口
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                    evt.StopPropagation();
                }
            }, TrickleDown.TrickleDown);

            root.focusable = true;
            root.RegisterCallback<MouseDownEvent>(_ => root.Focus());

            // 应用样式
            YokiStyleService.Apply(root, YokiStyleProfile.Full);

            // 创建主容器
            var mainContainer = new VisualElement();
            mainContainer.AddToClassList("root-container");
            root.Add(mainContainer);

            // 创建侧边栏
            var sidebar = CreateSidebar();
            mainContainer.Add(sidebar);

            // 创建内容区域
            mContentContainer = new();
            mContentContainer.AddToClassList("content-container");
            mainContainer.Add(mContentContainer);

            // 选择初始页面
            if (mPageInfos.Count > 0)
            {
                SelectPage(mSelectedPageIndex);
            }

            root.schedule.Execute(() => root.Focus()).ExecuteLater(100);
        }

        #endregion

        #region 页面管理

        /// <summary>
        /// 收集所有工具页面
        /// </summary>
        private void CollectPages()
        {
            mPageInfos.Clear();
            mPageInstances.Clear();
            mPageElements.Clear();
            mSidebarItems.Clear();

            // 从 Registry 获取页面信息
            foreach (var info in YokiToolPageRegistry.PageInfos)
            {
                mPageInfos.Add(info);
            }
        }

        /// <summary>
        /// 获取或创建页面实例
        /// </summary>
        private IYokiToolPage GetOrCreatePage(YokiPageInfo info)
        {
            if (mPageInstances.TryGetValue(info, out var existing))
            {
                return existing;
            }

            var page = YokiToolPageRegistry.GetOrCreatePage(info.PageType);
            mPageInstances[info] = page;
            return page;
        }

        #endregion
    }
}
#endif
