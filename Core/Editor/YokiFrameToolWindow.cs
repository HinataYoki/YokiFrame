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
    public class YokiFrameToolWindow : EditorWindow
    {
        private const string WINDOW_TITLE = "YokiFrame Tools";
        private const string STYLE_PATH = "Assets/YokiFrame/Core/Editor/Styles/YokiFrameToolStyles.uss";
        
        private readonly List<IYokiFrameToolPage> mPages = new();
        private readonly Dictionary<IYokiFrameToolPage, VisualElement> mPageElements = new();
        private readonly Dictionary<IYokiFrameToolPage, VisualElement> mSidebarItems = new();
        
        private int mSelectedPageIndex;
        private VisualElement mContentContainer;
        private IYokiFrameToolPage mActivePage;
        
        [MenuItem("YokiFrame/Tools Panel %e")]
        private static void Open()
        {
            var window = GetWindow<YokiFrameToolWindow>(false, WINDOW_TITLE);
            window.minSize = new Vector2(1000, 600);
            window.Show();
        }
        
        private void OnEnable()
        {
            CollectPages();
            mSelectedPageIndex = EditorPrefs.GetInt("YokiFrameTools_SelectedPage", 0);
            if (mSelectedPageIndex >= mPages.Count)
                mSelectedPageIndex = 0;
            
            EditorApplication.update += OnEditorUpdate;
        }
        
        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorPrefs.SetInt("YokiFrameTools_SelectedPage", mSelectedPageIndex);
            
            mActivePage?.OnDeactivate();
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            
            // 加载样式
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(STYLE_PATH);
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);
            
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
        
        private VisualElement CreateSidebar()
        {
            var sidebar = new VisualElement();
            sidebar.AddToClassList("sidebar");
            
            // 标题
            var header = new VisualElement();
            header.AddToClassList("sidebar-header");
            
            var title = new Label("YokiFrame");
            title.AddToClassList("sidebar-title");
            header.Add(title);
            sidebar.Add(header);
            
            // 页面列表
            var list = new ScrollView();
            list.AddToClassList("sidebar-list");
            
            for (int i = 0; i < mPages.Count; i++)
            {
                var page = mPages[i];
                var index = i;
                
                var item = new VisualElement();
                item.AddToClassList("sidebar-item");
                
                var label = new Label(page.PageName);
                label.AddToClassList("sidebar-item-label");
                item.Add(label);
                
                // 弹出按钮
                var popoutBtn = new Button(() => PopoutPage(page)) { text = "⧉" };
                popoutBtn.AddToClassList("sidebar-popout-btn");
                popoutBtn.tooltip = "在独立窗口中打开";
                item.Add(popoutBtn);
                
                // 点击选择页面（排除按钮区域）
                label.RegisterCallback<ClickEvent>(evt =>
                {
                    SelectPage(index);
                    evt.StopPropagation();
                });
                
                mSidebarItems[page] = item;
                list.Add(item);
            }
            
            sidebar.Add(list);
            return sidebar;
        }
        
        private void PopoutPage(IYokiFrameToolPage page)
        {
            // 创建新的页面实例用于独立窗口
            var newPage = (IYokiFrameToolPage)Activator.CreateInstance(page.GetType());
            YokiFrameToolPageWindow.Open(newPage);
        }
        
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
            
            // 更新侧边栏选中状态
            if (mSidebarItems.TryGetValue(page, out var newItem))
                newItem.AddToClassList("selected");
            
            // 显示页面内容
            mContentContainer.Clear();
            
            if (!mPageElements.TryGetValue(page, out var pageElement))
            {
                pageElement = page.CreateUI();
                mPageElements[page] = pageElement;
            }
            
            mContentContainer.Add(pageElement);
            page.OnActivate();
        }
        
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
        
        private void OnEditorUpdate()
        {
            mActivePage?.OnUpdate();
        }
    }
}
#endif
