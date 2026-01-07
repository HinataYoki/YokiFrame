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
        private VisualElement mSidebarHighlight;
        private VisualElement mSidebarListContainer;
        
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
            
            // 创建列表内容容器（用于放置高亮指示器）
            mSidebarListContainer = new VisualElement();
            mSidebarListContainer.style.position = Position.Relative;
            list.Add(mSidebarListContainer);
            
            // 创建高亮指示器
            mSidebarHighlight = new VisualElement();
            mSidebarHighlight.style.position = Position.Absolute;
            mSidebarHighlight.style.borderTopLeftRadius = 6;
            mSidebarHighlight.style.borderTopRightRadius = 6;
            mSidebarHighlight.style.borderBottomLeftRadius = 6;
            mSidebarHighlight.style.borderBottomRightRadius = 6;
            mSidebarHighlight.style.opacity = 0;
            mSidebarHighlight.pickingMode = PickingMode.Ignore;
            // 添加过渡动画
            mSidebarHighlight.style.transitionProperty = new List<StylePropertyName> 
            { 
                new("top"), 
                new("left"), 
                new("width"), 
                new("height"),
                new("opacity"),
                new("background-color")
            };
            mSidebarHighlight.style.transitionDuration = new List<TimeValue> 
            { 
                new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond),
                new(150, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond)
            };
            mSidebarHighlight.style.transitionTimingFunction = new List<EasingFunction>
            {
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut)
            };
            mSidebarListContainer.Add(mSidebarHighlight);
            
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
                mSidebarListContainer.Add(docsGroup);
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
                mSidebarListContainer.Add(toolsGroup);
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
            var icon = new Label(page.PageIcon);
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
            
            // 更新侧边栏选中状态并移动高亮指示器
            if (mSidebarItems.TryGetValue(page, out var newItem))
            {
                newItem.AddToClassList("selected");
                
                // 判断是文档还是工具页面，设置对应的高亮颜色
                bool isDocPage = page is DocumentationToolPage;
                var highlightColor = isDocPage 
                    ? new Color(0.39f, 0.63f, 1f, 0.2f)  // 文档蓝色
                    : new Color(1f, 0.71f, 0.39f, 0.2f); // 工具橙色
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
        /// 将侧边栏高亮指示器平滑移动到目标项
        /// </summary>
        private void MoveSidebarHighlight(VisualElement targetItem)
        {
            if (targetItem == null || mSidebarHighlight == null || mSidebarListContainer == null) return;
            
            // 获取目标项相对于容器的位置
            var targetRect = targetItem.worldBound;
            var containerRect = mSidebarListContainer.worldBound;
            
            // 计算相对位置
            float relativeTop = targetRect.y - containerRect.y;
            float relativeLeft = targetRect.x - containerRect.x;
            
            // 设置高亮指示器位置和大小
            mSidebarHighlight.style.top = relativeTop;
            mSidebarHighlight.style.left = relativeLeft;
            mSidebarHighlight.style.width = targetRect.width;
            mSidebarHighlight.style.height = targetRect.height;
            mSidebarHighlight.style.opacity = 1;
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
