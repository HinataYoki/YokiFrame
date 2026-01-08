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
            
            // 页面列表（带分组）- 隐藏水平滚动条
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList("sidebar-list");
            list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            list.verticalScrollerVisibility = ScrollerVisibility.Auto;
            
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
            
            // 底部版本信息区域
            sidebar.Add(CreateVersionInfoPanel());
            
            return sidebar;
        }
        
        /// <summary>
        /// 创建版本信息面板
        /// </summary>
        private VisualElement CreateVersionInfoPanel()
        {
            var versionPanel = new VisualElement();
            versionPanel.style.paddingLeft = 16;
            versionPanel.style.paddingRight = 16;
            versionPanel.style.paddingTop = 12;
            versionPanel.style.paddingBottom = 16;
            versionPanel.style.borderTopWidth = 1;
            versionPanel.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
            versionPanel.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.09f));
            
            // 读取 package.json 获取版本
            string version = GetPackageVersion();
            
            // 版本行
            var versionRow = new VisualElement();
            versionRow.style.flexDirection = FlexDirection.Row;
            versionRow.style.alignItems = Align.Center;
            versionRow.style.marginBottom = 8;
            
            var versionIcon = new Label("📦");
            versionIcon.style.fontSize = 12;
            versionIcon.style.marginRight = 8;
            versionRow.Add(versionIcon);
            
            var versionLabel = new Label("YokiFrame");
            versionLabel.style.fontSize = 12;
            versionLabel.style.color = new StyleColor(new Color(0.75f, 0.75f, 0.78f));
            versionLabel.style.flexGrow = 1;
            versionRow.Add(versionLabel);
            
            var versionBadge = new Label($"v{version}");
            versionBadge.style.fontSize = 10;
            versionBadge.style.color = new StyleColor(new Color(0.34f, 0.61f, 0.84f));
            versionBadge.style.backgroundColor = new StyleColor(new Color(0.2f, 0.3f, 0.45f, 0.35f));
            versionBadge.style.paddingLeft = 6;
            versionBadge.style.paddingRight = 6;
            versionBadge.style.paddingTop = 2;
            versionBadge.style.paddingBottom = 2;
            versionBadge.style.borderTopLeftRadius = 4;
            versionBadge.style.borderTopRightRadius = 4;
            versionBadge.style.borderBottomLeftRadius = 4;
            versionBadge.style.borderBottomRightRadius = 4;
            versionRow.Add(versionBadge);
            
            versionPanel.Add(versionRow);
            
            // GitHub 链接
            var linkRow = new VisualElement();
            linkRow.style.flexDirection = FlexDirection.Row;
            linkRow.style.alignItems = Align.Center;
            linkRow.style.paddingTop = 6;
            linkRow.style.paddingBottom = 6;
            linkRow.style.paddingLeft = 4;
            linkRow.style.borderTopLeftRadius = 4;
            linkRow.style.borderTopRightRadius = 4;
            linkRow.style.borderBottomLeftRadius = 4;
            linkRow.style.borderBottomRightRadius = 4;
            linkRow.style.transitionProperty = new List<StylePropertyName> { new("background-color") };
            linkRow.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };
            
            var linkIcon = new Label("🔗");
            linkIcon.style.fontSize = 11;
            linkIcon.style.marginRight = 8;
            linkRow.Add(linkIcon);
            
            var linkLabel = new Label("GitHub");
            linkLabel.style.fontSize = 11;
            linkLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.55f));
            linkLabel.style.transitionProperty = new List<StylePropertyName> { new("color") };
            linkLabel.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };
            linkRow.Add(linkLabel);
            
            linkRow.RegisterCallback<MouseEnterEvent>(evt =>
            {
                linkRow.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f));
                linkLabel.style.color = new StyleColor(new Color(0.34f, 0.61f, 0.84f));
            });
            linkRow.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                linkRow.style.backgroundColor = new StyleColor(Color.clear);
                linkLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.55f));
            });
            linkRow.RegisterCallback<ClickEvent>(evt =>
            {
                Application.OpenURL("https://github.com/HinataYoki/YokiFrame");
            });
            
            versionPanel.Add(linkRow);
            
            return versionPanel;
        }
        
        /// <summary>
        /// 从 package.json 读取版本号
        /// </summary>
        private string GetPackageVersion()
        {
            const string DEFAULT_VERSION = "1.0.0";
            string packagePath = "Assets/YokiFrame/package.json";
            
            if (!System.IO.File.Exists(packagePath)) return DEFAULT_VERSION;
            
            try
            {
                string json = System.IO.File.ReadAllText(packagePath);
                int versionIndex = json.IndexOf("\"version\"");
                if (versionIndex < 0) return DEFAULT_VERSION;
                
                int colonIndex = json.IndexOf(':', versionIndex);
                int startQuote = json.IndexOf('"', colonIndex);
                int endQuote = json.IndexOf('"', startQuote + 1);
                
                if (startQuote >= 0 && endQuote > startQuote)
                {
                    return json.Substring(startQuote + 1, endQuote - startQuote - 1);
                }
            }
            catch
            {
                // 忽略解析错误
            }
            
            return DEFAULT_VERSION;
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
                // 统一使用品牌蓝色作为高亮色
                var highlightColor = new Color(0.13f, 0.59f, 0.95f, 0.12f);  // 品牌蓝 #2196F3
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
