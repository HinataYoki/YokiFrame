#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 工具总面板
    /// </summary>
    public partial class YokiFrameToolWindow : EditorWindow
    {
        #region 常量

        private const string WINDOW_TITLE = "YokiFrame Tools";
        private const string ICON_PATH = "yoki";

        #endregion

        #region 静态字段

        private static Texture2D sIconTexture;

        #endregion

        #region 实例字段

        private readonly List<IYokiFrameToolPage> mPages = new();
        private readonly Dictionary<IYokiFrameToolPage, VisualElement> mPageElements = new();
        private readonly Dictionary<IYokiFrameToolPage, VisualElement> mSidebarItems = new();

        private int mSelectedPageIndex;
        private VisualElement mContentContainer;
        private IYokiFrameToolPage mActivePage;
        private VisualElement mSidebarHighlight;
        private VisualElement mSidebarListContainer;

        #endregion

        #region 页面分类

        private enum PageCategory
        {
            Documentation,
            Tool
        }

        #endregion

        #region 窗口入口

        [MenuItem("YokiFrame/Tools Panel %e")]
        private static void Open()
        {
            var window = GetWindow<YokiFrameToolWindow>(false, WINDOW_TITLE);
            window.minSize = new Vector2(1000, 600);
            window.titleContent = new GUIContent(WINDOW_TITLE, LoadIcon());
            window.Show();
        }

        private static Texture2D LoadIcon()
        {
            if (sIconTexture == null)
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
            mSelectedPageIndex = EditorPrefs.GetInt("YokiFrameTools_SelectedPage", 0);
            if (mSelectedPageIndex >= mPages.Count)
                mSelectedPageIndex = 0;

            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorPrefs.SetInt("YokiFrameTools_SelectedPage", mSelectedPageIndex);

            mActivePage?.OnDeactivate();
        }

        /// <summary>
        /// PlayMode 状态变化时清理缓存并重建当前页面
        /// </summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // 进入或退出 PlayMode 后重建当前页面
            if (state == PlayModeStateChange.EnteredPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                // 延迟执行，确保 Unity 状态已完全切换
                EditorApplication.delayCall += () =>
                {
                    if (this == null || mContentContainer == null) return;

                    // 清理页面元素缓存，强制重建
                    mPageElements.Clear();

                    // 重新选择当前页面
                    if (mActivePage != null && mSelectedPageIndex >= 0 && mSelectedPageIndex < mPages.Count)
                    {
                        // 先停用再重新激活
                        mActivePage.OnDeactivate();
                        mActivePage = null;
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

            // 加载样式
            YokiFrameEditorUtility.ApplyMainStyleSheet(root);

            // 创建主容器
            var mainContainer = new VisualElement();
            mainContainer.AddToClassList("root-container");
            root.Add(mainContainer);

            // 创建侧边栏
            var sidebar = CreateSidebar();
            mainContainer.Add(sidebar);

            // 创建内容区域
            mContentContainer = new VisualElement();
            mContentContainer.AddToClassList("content-container");
            mainContainer.Add(mContentContainer);

            // 选择初始页面
            if (mPages.Count > 0)
            {
                SelectPage(mSelectedPageIndex);
            }
        }

        #endregion

        #region 页面管理

        /// <summary>
        /// 收集所有工具页面
        /// </summary>
        private void CollectPages()
        {
            mPages.Clear();
            mPageElements.Clear();
            mSidebarItems.Clear();

            var pageType = typeof(IYokiFrameToolPage);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsAbstract || type.IsInterface)
                            continue;
                        if (!pageType.IsAssignableFrom(type))
                            continue;

                        var page = (IYokiFrameToolPage)Activator.CreateInstance(type);
                        mPages.Add(page);
                    }
                }
                catch
                {
                    // 忽略无法加载的程序集
                }
            }

            mPages.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        /// <summary>
        /// 选择页面
        /// </summary>
        private void SelectPage(int index)
        {
            if (index < 0 || index >= mPages.Count) return;

            // 停用当前页面
            if (mActivePage != null)
            {
                mActivePage.OnDeactivate();
                if (mSidebarItems.TryGetValue(mActivePage, out var oldItem))
                    oldItem.RemoveFromClassList("selected");
            }

            mSelectedPageIndex = index;
            var page = mPages[index];
            mActivePage = page;

            // 更新侧边栏选中状态并移动高亮指示器
            if (mSidebarItems.TryGetValue(page, out var newItem))
            {
                newItem.AddToClassList("selected");

                // 统一使用品牌蓝色作为高亮色
                var highlightColor = new Color(0.13f, 0.59f, 0.95f, 0.12f);
                mSidebarHighlight.style.backgroundColor = new StyleColor(highlightColor);

                // 延迟一帧获取正确的布局位置
                newItem.schedule.Execute(() => MoveSidebarHighlight(newItem)).ExecuteLater(1);
            }

            // 显示页面内容（带淡入动画）
            mContentContainer.Clear();

            if (!mPageElements.TryGetValue(page, out var pageElement))
            {
                pageElement = page.CreateUI();
                mPageElements[page] = pageElement;
            }

            // 添加淡入动画初始状态
            pageElement.AddToClassList("content-fade-in");
            pageElement.RemoveFromClassList("content-visible");

            mContentContainer.Add(pageElement);
            page.OnActivate();

            // 延迟一帧后添加可见类，触发动画
            pageElement.schedule.Execute(() =>
            {
                pageElement.AddToClassList("content-visible");
            }).ExecuteLater(16);
        }

        /// <summary>
        /// 弹出页面到独立窗口
        /// </summary>
        private void PopoutPage(IYokiFrameToolPage page)
        {
            var newPage = (IYokiFrameToolPage)Activator.CreateInstance(page.GetType());
            YokiFrameToolPageWindow.Open(newPage);
        }

        #endregion
    }
}
#endif
