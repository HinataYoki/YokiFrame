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
        private const string ICON_PATH = "yoki";
        
        private static Texture2D sIconTexture;
        
        private readonly List<IYokiFrameToolPage> mPages = new();
        private readonly Dictionary<IYokiFrameToolPage, VisualElement> mPageElements = new();
        private readonly Dictionary<IYokiFrameToolPage, VisualElement> mSidebarItems = new();
        
        private int mSelectedPageIndex;
        private VisualElement mContentContainer;
        private IYokiFrameToolPage mActivePage;
        
        // 页面分类
        private enum PageCategory
        {
            Documentation,
            Tool
        }
        
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
        
        private VisualElement CreateSidebar()
        {
            var sidebar = new VisualElement();
            sidebar.AddToClassList("sidebar");
            
            // 标题
            var header = new VisualElement();
            header.AddToClassList("sidebar-header");
            header.style.flexDirection = FlexDirection.Column;
            header.style.alignItems = Align.Center;
            
            // 添加框架图标 - 居中突出显示
            Texture2D iconTexture = LoadIcon();
            if (iconTexture != null)
            {
                var iconImage = new Image { image = iconTexture };
                iconImage.style.width = 64;
                iconImage.style.height = 64;
                iconImage.style.marginBottom = 8;
                header.Add(iconImage);
            }
            
            var title = new Label("YokiFrame");
            title.AddToClassList("sidebar-title");
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.Add(title);
            sidebar.Add(header);
            
            // 页面列表（带分组）
            var list = new ScrollView();
            list.AddToClassList("sidebar-list");
            
            // 分离文档和工具页面
            var docPages = new List<(int index, IYokiFrameToolPage page)>();
            var toolPages = new List<(int index, IYokiFrameToolPage page)>();
            
            for (int i = 0; i < mPages.Count; i++)
            {
                var page = mPages[i];
                if (page is DocumentationToolPage)
                    docPages.Add((i, page));
                else
                    toolPages.Add((i, page));
            }
            
            // 文档分组
            if (docPages.Count > 0)
            {
                var docsGroup = CreateSidebarGroup("📖", "文档", docPages.Count, "docs");
                foreach (var (index, page) in docPages)
                {
                    var item = CreateSidebarItem(page, index, "docs");
                    docsGroup.Add(item);
                }
                list.Add(docsGroup);
            }
            
            // 工具分组
            if (toolPages.Count > 0)
            {
                var toolsGroup = CreateSidebarGroup("🔧", "工具", toolPages.Count, "tools");
                foreach (var (index, page) in toolPages)
                {
                    var item = CreateSidebarItem(page, index, "tools");
                    toolsGroup.Add(item);
                }
                list.Add(toolsGroup);
            }
            
            sidebar.Add(list);
            return sidebar;
        }
        
        private VisualElement CreateSidebarGroup(string icon, string title, int count, string groupClass)
        {
            var group = new VisualElement();
            group.AddToClassList("sidebar-group");
            group.AddToClassList(groupClass);
            
            var header = new VisualElement();
            header.AddToClassList("sidebar-group-header");
            
            var iconLabel = new Label(icon);
            iconLabel.AddToClassList("sidebar-group-icon");
            header.Add(iconLabel);
            
            var titleLabel = new Label(title.ToUpper());
            titleLabel.AddToClassList("sidebar-group-title");
            header.Add(titleLabel);
            
            var countLabel = new Label(count.ToString());
            countLabel.AddToClassList("sidebar-group-count");
            header.Add(countLabel);
            
            group.Add(header);
            return group;
        }
        
        private VisualElement CreateSidebarItem(IYokiFrameToolPage page, int index, string groupClass)
        {
            var item = new VisualElement();
            item.AddToClassList("sidebar-item");
            
            // 图标
            var icon = new Label(GetPageIcon(page));
            icon.AddToClassList("sidebar-item-icon");
            item.Add(icon);
            
            var label = new Label(page.PageName);
            label.AddToClassList("sidebar-item-label");
            item.Add(label);
            
            // 弹出按钮
            var popoutBtn = new Button(() => PopoutPage(page)) { text = "⧉" };
            popoutBtn.AddToClassList("sidebar-popout-btn");
            popoutBtn.tooltip = "在独立窗口中打开";
            item.Add(popoutBtn);
            
            // 点击选择页面
            item.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == popoutBtn) return;
                SelectPage(index);
                evt.StopPropagation();
            });
            
            mSidebarItems[page] = item;
            return item;
        }
        
        private string GetPageIcon(IYokiFrameToolPage page)
        {
            return page.PageName switch
            {
                "文档" => "📚",
                "EventKit" => "📡",
                "FsmKit" => "🔄",
                "UIKit" => "🖼️",
                "AudioKit" => "🔊",
                _ => "📦"
            };
        }
        
        private void PopoutPage(IYokiFrameToolPage page)
        {
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
