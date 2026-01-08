#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 文档页面 - 带语法高亮的详细 API 文档
    /// </summary>
    public partial class DocumentationToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "文档";
        public override string PageIcon => KitIcons.DOCUMENTATION;
        public override int Priority => 0;
        
        private ScrollView mTocScrollView;
        private ScrollView mContentScrollView;
        private readonly List<DocModule> mModules = new();
        private readonly Dictionary<VisualElement, int> mTocItemMap = new();
        private VisualElement mSelectedTocItem;
        private VisualElement mHighlightIndicator;
        private VisualElement mTocItemsContainer;
        
        // 右侧本页导航
        private VisualElement mOnThisPagePanel;
        private VisualElement mOnThisPageContainer;
        private readonly List<(string title, VisualElement element, int level)> mCurrentHeadings = new();
        private VisualElement mSelectedHeadingItem;
        
        // 导航项与内容元素的映射（用于滚动同步）
        private readonly List<(VisualElement navItem, VisualElement contentElement)> mHeadingNavMap = new();
        private bool mIsScrollingByClick; // 防止点击滚动时触发滚动监听
        
        // 响应式布局阈值
        private const float ON_THIS_PAGE_MIN_WIDTH = 1200f;
        
        // 颜色主题
        private static class Theme
        {
            // 背景色
            public static readonly Color BgPrimary = new(0.16f, 0.16f, 0.16f);
            public static readonly Color BgSecondary = new(0.14f, 0.14f, 0.14f);
            public static readonly Color BgTertiary = new(0.12f, 0.12f, 0.12f);
            public static readonly Color BgCode = new(0.1f, 0.1f, 0.1f);
            public static readonly Color BgHover = new(0.2f, 0.2f, 0.2f);
            public static readonly Color BgSelected = new(0.24f, 0.37f, 0.58f);
            
            // 强调色
            public static readonly Color AccentBlue = new(0.34f, 0.61f, 0.84f);
            public static readonly Color AccentGreen = new(0.4f, 0.7f, 0.4f);
            public static readonly Color AccentOrange = new(0.9f, 0.6f, 0.3f);
            public static readonly Color AccentPurple = new(0.7f, 0.5f, 0.8f);
            public static readonly Color AccentRed = new(0.9f, 0.4f, 0.4f);
            public static readonly Color AccentYellow = new(0.9f, 0.8f, 0.4f);
            
            // 文字色
            public static readonly Color TextPrimary = new(0.95f, 0.95f, 0.95f);
            public static readonly Color TextSecondary = new(0.8f, 0.8f, 0.8f);
            public static readonly Color TextMuted = new(0.6f, 0.6f, 0.6f);
            public static readonly Color TextDim = new(0.5f, 0.5f, 0.5f);
            
            // 边框色
            public static readonly Color Border = new(0.25f, 0.25f, 0.25f);
            public static readonly Color BorderDark = new(0.1f, 0.1f, 0.1f);
            
            // 分类颜色（扁平化低饱和度）
            public static readonly Color CategoryCore = new(0.55f, 0.7f, 0.85f);
            public static readonly Color CategoryKit = new(0.55f, 0.75f, 0.6f);
            public static readonly Color CategoryTools = new(0.85f, 0.7f, 0.55f);
            
            // 分类背景色（与整体灰色协调）
            public static readonly Color CategoryCoreBg = new(0.14f, 0.15f, 0.17f);
            public static readonly Color CategoryKitBg = new(0.14f, 0.16f, 0.15f);
            public static readonly Color CategoryToolsBg = new(0.16f, 0.15f, 0.14f);
        }
        
        protected override void BuildUI(VisualElement root)
        {
            InitializeDocumentation();
            
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexGrow = 1;
            root.Add(container);
            
            // 左侧目录面板
            container.Add(CreateTocPanel());
            
            // 中间内容区域
            mContentScrollView = new ScrollView();
            mContentScrollView.style.flexGrow = 1;
            mContentScrollView.style.backgroundColor = new StyleColor(Theme.BgPrimary);
            mContentScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            
            // 监听滚动事件，同步更新右侧导航高亮
            mContentScrollView.verticalScroller.valueChanged += OnContentScrollChanged;
            
            container.Add(mContentScrollView);
            
            // 右侧本页导航面板
            container.Add(CreateOnThisPagePanel());
            
            // 监听窗口大小变化，响应式显示/隐藏右侧面板
            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            
            if (mModules.Count > 0) SelectModule(0);
        }
        
        /// <summary>
        /// 响应式布局：根据窗口宽度显示/隐藏右侧导航
        /// </summary>
        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            if (mOnThisPagePanel == null) return;
            
            bool shouldShow = evt.newRect.width >= ON_THIS_PAGE_MIN_WIDTH;
            mOnThisPagePanel.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        /// <summary>
        /// 创建右侧"本页导航"面板
        /// </summary>
        private VisualElement CreateOnThisPagePanel()
        {
            mOnThisPagePanel = new VisualElement();
            mOnThisPagePanel.style.width = 200;
            mOnThisPagePanel.style.minWidth = 180;
            mOnThisPagePanel.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.12f));
            mOnThisPagePanel.style.borderLeftWidth = 1;
            mOnThisPagePanel.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.05f));
            mOnThisPagePanel.style.paddingTop = 24;
            mOnThisPagePanel.style.paddingLeft = 20;
            mOnThisPagePanel.style.paddingRight = 16;
            mOnThisPagePanel.style.display = DisplayStyle.None;
            
            // 标题
            var title = new Label("本页目录");
            title.style.fontSize = 12;
            title.style.color = new StyleColor(Theme.TextMuted);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.letterSpacing = 1f;
            title.style.marginBottom = 20;
            mOnThisPagePanel.Add(title);
            
            // 导航项容器
            mOnThisPageContainer = new VisualElement();
            mOnThisPagePanel.Add(mOnThisPageContainer);
            
            return mOnThisPagePanel;
        }
        
        /// <summary>
        /// 刷新右侧本页导航
        /// </summary>
        private void RefreshOnThisPage()
        {
            if (mOnThisPageContainer == null) return;
            
            mOnThisPageContainer.Clear();
            mSelectedHeadingItem = null;
            mHeadingNavMap.Clear();
            
            bool isFirst = true;
            foreach (var (headingTitle, element, level) in mCurrentHeadings)
            {
                var item = CreateOnThisPageItem(headingTitle, element, level, isFirst);
                mOnThisPageContainer.Add(item);
                
                // 记录导航项与内容元素的映射
                mHeadingNavMap.Add((item, element));
                
                // 默认高亮第一项
                if (isFirst)
                {
                    mSelectedHeadingItem = item;
                    isFirst = false;
                }
            }
        }
        
        /// <summary>
        /// 内容滚动时同步更新右侧导航高亮
        /// </summary>
        private void OnContentScrollChanged(float scrollValue)
        {
            // 如果是点击导航项触发的滚动，跳过处理
            if (mIsScrollingByClick || mHeadingNavMap.Count == 0) return;
            
            // 获取 ScrollView 的可视区域顶部位置
            var scrollViewRect = mContentScrollView.contentContainer.worldBound;
            float viewportTop = scrollViewRect.y + scrollValue;
            float threshold = 80f; // 距离顶部多少像素时认为进入该章节
            
            VisualElement activeNavItem = null;
            
            // 从后往前遍历，找到第一个已经滚动过顶部的章节
            for (int i = mHeadingNavMap.Count - 1; i >= 0; i--)
            {
                var (navItem, contentElement) = mHeadingNavMap[i];
                var elementRect = contentElement.worldBound;
                
                // 如果该章节的顶部已经滚动到视口顶部附近或以上
                if (elementRect.y <= viewportTop + threshold)
                {
                    activeNavItem = navItem;
                    break;
                }
            }
            
            // 如果没找到（说明还在最顶部），默认选中第一项
            if (activeNavItem == null && mHeadingNavMap.Count > 0)
            {
                activeNavItem = mHeadingNavMap[0].navItem;
            }
            
            // 更新高亮状态
            if (activeNavItem != null && activeNavItem != mSelectedHeadingItem)
            {
                UpdateHeadingHighlight(activeNavItem);
            }
        }
        
        /// <summary>
        /// 更新右侧导航的高亮状态
        /// </summary>
        private void UpdateHeadingHighlight(VisualElement newActiveItem)
        {
            // 清除旧的高亮
            if (mSelectedHeadingItem != null)
            {
                mSelectedHeadingItem.style.borderLeftColor = new StyleColor(Color.clear);
                mSelectedHeadingItem.style.backgroundColor = new StyleColor(Color.clear);
                var prevLabel = mSelectedHeadingItem.Q<Label>();
                if (prevLabel != null) prevLabel.style.color = new StyleColor(Theme.TextMuted);
            }
            
            // 设置新的高亮
            mSelectedHeadingItem = newActiveItem;
            newActiveItem.style.borderLeftColor = new StyleColor(Theme.AccentBlue);
            newActiveItem.style.backgroundColor = new StyleColor(new Color(0.24f, 0.37f, 0.58f, 0.35f));
            var newLabel = newActiveItem.Q<Label>();
            if (newLabel != null) newLabel.style.color = new StyleColor(Theme.TextPrimary);
        }
        
        /// <summary>
        /// 创建本页导航项
        /// </summary>
        private VisualElement CreateOnThisPageItem(string title, VisualElement targetElement, int level, bool isActive = false)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingTop = 8;  // 增大间距
            item.style.paddingBottom = 8;
            item.style.paddingLeft = level == 2 ? 14 : 6; // H2 缩进
            item.style.paddingRight = 6;
            item.style.marginTop = 2;
            item.style.marginBottom = 2;
            item.style.borderTopLeftRadius = 4;
            item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4;
            item.style.borderBottomRightRadius = 4;
            item.style.borderLeftWidth = 2;
            item.style.borderLeftColor = isActive ? new StyleColor(Theme.AccentBlue) : new StyleColor(Color.clear);
            // 当前选中项背景色（较亮）
            item.style.backgroundColor = isActive ? new StyleColor(new Color(0.24f, 0.37f, 0.58f, 0.35f)) : new StyleColor(Color.clear);
            item.style.transitionProperty = new List<StylePropertyName> { new("border-left-color"), new("background-color") };
            item.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond), new(150, TimeUnit.Millisecond) };
            
            var label = new Label(title);
            label.style.fontSize = level == 1 ? 14 : 13; // 增大字号
            label.style.color = isActive ? new StyleColor(Theme.TextPrimary) : new StyleColor(Theme.TextMuted); // 激活态文字更亮
            label.style.transitionProperty = new List<StylePropertyName> { new("color") };
            label.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };
            item.Add(label);
            
            // 悬停效果 - 添加背景色高亮（比选中态稍暗）
            item.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (item != mSelectedHeadingItem)
                {
                    label.style.color = new StyleColor(Theme.TextSecondary);
                    item.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.25f, 0.6f)); // 悬停背景色（较暗）
                }
            });
            item.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (item != mSelectedHeadingItem)
                {
                    label.style.color = new StyleColor(Theme.TextMuted);
                    item.style.backgroundColor = new StyleColor(Color.clear);
                }
            });
            
            // 点击滚动到对应位置
            item.RegisterCallback<ClickEvent>(evt =>
            {
                // 设置标记，防止滚动监听触发
                mIsScrollingByClick = true;
                
                // 更新高亮状态
                UpdateHeadingHighlight(item);
                
                // 滚动到目标位置
                mContentScrollView.ScrollTo(targetElement);
                
                // 延迟重置标记，等待滚动完成
                item.schedule.Execute(() => mIsScrollingByClick = false).ExecuteLater(300);
            });
            
            return item;
        }
        
        private VisualElement CreateTocPanel()
        {
            var panel = new VisualElement();
            panel.style.width = 260;
            panel.style.minWidth = 240;
            panel.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.11f)); // 更透明的背景
            panel.style.borderRightWidth = 1;
            panel.style.borderRightColor = new StyleColor(new Color(0.18f, 0.18f, 0.22f, 0.6f));
            
            mTocScrollView = new ScrollView();
            mTocScrollView.style.flexGrow = 1;
            mTocScrollView.style.paddingTop = 16;
            mTocScrollView.style.paddingBottom = 16;
            panel.Add(mTocScrollView);
            
            // 创建高亮指示器（独立元素，用于平滑移动动画）
            mHighlightIndicator = new VisualElement();
            mHighlightIndicator.style.position = Position.Absolute;
            mHighlightIndicator.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.25f));
            mHighlightIndicator.style.borderTopLeftRadius = 6;
            mHighlightIndicator.style.borderTopRightRadius = 6;
            mHighlightIndicator.style.borderBottomLeftRadius = 6;
            mHighlightIndicator.style.borderBottomRightRadius = 6;
            mHighlightIndicator.style.opacity = 0;
            mHighlightIndicator.pickingMode = PickingMode.Ignore;
            // 添加过渡动画
            mHighlightIndicator.style.transitionProperty = new List<StylePropertyName> 
            { 
                new("top"), 
                new("left"), 
                new("width"), 
                new("height"),
                new("opacity")
            };
            mHighlightIndicator.style.transitionDuration = new List<TimeValue> 
            { 
                new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond),
                new(150, TimeUnit.Millisecond)
            };
            mHighlightIndicator.style.transitionTimingFunction = new List<EasingFunction>
            {
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut)
            };
            
            RefreshToc();
            return panel;
        }
        
        private void RefreshToc()
        {
            mTocScrollView.Clear();
            mTocItemMap.Clear();
            
            // 创建内容容器（用于放置高亮指示器）
            mTocItemsContainer = new VisualElement();
            mTocItemsContainer.style.position = Position.Relative;
            mTocScrollView.Add(mTocItemsContainer);
            
            // 添加高亮指示器到容器
            mTocItemsContainer.Add(mHighlightIndicator);
            
            string currentCategory = null;
            VisualElement categoryGroup = null;
            
            for (int i = 0; i < mModules.Count; i++)
            {
                var module = mModules[i];
                var moduleIndex = i;
                
                if (module.Category != currentCategory)
                {
                    currentCategory = module.Category;
                    
                    // 创建分类组容器（扁平化风格 - 去掉卡片背景）
                    categoryGroup = new VisualElement();
                    categoryGroup.style.marginTop = i == 0 ? 0 : 16;
                    categoryGroup.style.marginLeft = 8;
                    categoryGroup.style.marginRight = 8;
                    categoryGroup.style.marginBottom = 4;
                    
                    var categoryColor = GetCategoryColor(currentCategory);
                    
                    // 分类标题栏
                    var categoryHeader = new VisualElement();
                    categoryHeader.style.flexDirection = FlexDirection.Row;
                    categoryHeader.style.alignItems = Align.Center;
                    categoryHeader.style.paddingLeft = 8;
                    categoryHeader.style.paddingRight = 8;
                    categoryHeader.style.paddingTop = 8;
                    categoryHeader.style.paddingBottom = 8;
                    
                    // 分类图标
                    var categoryIcon = new Label(GetCategoryIcon(currentCategory));
                    categoryIcon.style.fontSize = 12;
                    categoryIcon.style.marginRight = 6;
                    categoryHeader.Add(categoryIcon);
                    
                    // 分类标签
                    var categoryLabel = new Label(currentCategory);
                    categoryLabel.style.fontSize = 11;
                    categoryLabel.style.color = new StyleColor(new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.8f));
                    categoryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    categoryLabel.style.flexGrow = 1;
                    categoryLabel.style.letterSpacing = 1f;
                    categoryHeader.Add(categoryLabel);
                    
                    // 分类徽章（显示数量）
                    var countBadge = new Label(GetCategoryModuleCount(currentCategory).ToString());
                    countBadge.style.fontSize = 10;
                    countBadge.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.65f));
                    countBadge.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f));
                    countBadge.style.paddingLeft = 6;
                    countBadge.style.paddingRight = 6;
                    countBadge.style.paddingTop = 2;
                    countBadge.style.paddingBottom = 2;
                    countBadge.style.borderTopLeftRadius = 8;
                    countBadge.style.borderTopRightRadius = 8;
                    countBadge.style.borderBottomLeftRadius = 8;
                    countBadge.style.borderBottomRightRadius = 8;
                    categoryHeader.Add(countBadge);
                    
                    categoryGroup.Add(categoryHeader);
                    mTocItemsContainer.Add(categoryGroup);
                }
                
                var item = CreateTocItem(module, moduleIndex);
                categoryGroup.Add(item);
            }
        }
        
        private VisualElement CreateTocItem(DocModule module, int index)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingLeft = 10;
            item.style.paddingRight = 8;
            item.style.paddingTop = 8;
            item.style.paddingBottom = 8;
            item.style.marginLeft = 4;
            item.style.marginRight = 4;
            item.style.marginTop = 1;
            item.style.marginBottom = 1;
            item.style.borderTopLeftRadius = 6;
            item.style.borderTopRightRadius = 6;
            item.style.borderBottomLeftRadius = 6;
            item.style.borderBottomRightRadius = 6;
            // 左侧蓝色竖条（选中态呼应）
            item.style.borderLeftWidth = 3;
            item.style.borderLeftColor = new StyleColor(Color.clear);
            
            // 添加过渡动画
            item.style.transitionProperty = new List<StylePropertyName>
            {
                new("background-color"),
                new("border-left-color")
            };
            item.style.transitionDuration = new List<TimeValue>
            {
                new(150, TimeUnit.Millisecond),
                new(150, TimeUnit.Millisecond)
            };
            item.style.transitionTimingFunction = new List<EasingFunction>
            {
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut)
            };
            
            // 简化的图标
            var icon = new Label(module.Icon);
            icon.style.fontSize = 15;
            icon.style.width = 24;
            icon.style.marginRight = 8;
            icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            icon.style.transitionProperty = new List<StylePropertyName> { new("scale") };
            icon.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };
            item.Add(icon);
            
            var label = new Label(module.Name);
            label.style.fontSize = 13;
            label.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.75f));
            label.style.flexGrow = 1;
            label.style.transitionProperty = new List<StylePropertyName> { new("color") };
            label.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };
            item.Add(label);
            
            // 简化的箭头指示器
            var arrow = new Label("›");
            arrow.style.fontSize = 15;
            arrow.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.45f));
            arrow.name = "arrow";
            arrow.style.transitionProperty = new List<StylePropertyName> { new("color") };
            arrow.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };
            item.Add(arrow);
            
            mTocItemMap[item] = index;
            
            item.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (item != mSelectedTocItem)
                {
                    item.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f));
                    
                    var arrowLabel = item.Q<Label>("arrow");
                    if (arrowLabel != null) arrowLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.65f));
                    
                    var iconLabel = item.ElementAt(0) as Label;
                    if (iconLabel != null) iconLabel.style.scale = new Scale(new Vector3(1.1f, 1.1f, 1f));
                }
            });
            item.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (item != mSelectedTocItem)
                {
                    item.style.backgroundColor = StyleKeyword.Null;
                    item.style.borderLeftColor = new StyleColor(Color.clear);
                    
                    var arrowLabel = item.Q<Label>("arrow");
                    if (arrowLabel != null) arrowLabel.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.45f));
                    
                    var textLabel = item.ElementAt(1) as Label;
                    if (textLabel != null) textLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.75f));
                    
                    var iconLabel = item.ElementAt(0) as Label;
                    if (iconLabel != null) iconLabel.style.scale = new Scale(Vector3.one);
                }
            });
            item.RegisterCallback<ClickEvent>(evt => SelectModule(index));
            
            return item;
        }
        
        private Color GetCategoryColor(string category)
        {
            return category switch
            {
                "CORE" => Theme.CategoryCore,
                "CORE KIT" => Theme.CategoryKit,
                "TOOLS" => Theme.CategoryTools,
                _ => Theme.AccentBlue
            };
        }
        
        private Color GetCategoryBgColor(string category)
        {
            return category switch
            {
                "CORE" => Theme.CategoryCoreBg,
                "CORE KIT" => Theme.CategoryKitBg,
                "TOOLS" => Theme.CategoryToolsBg,
                _ => Theme.BgTertiary
            };
        }
        
        private string GetCategoryIcon(string category)
        {
            return category switch
            {
                "CORE" => "⚙️",
                "CORE KIT" => "🧩",
                "TOOLS" => "🔧",
                _ => "📦"
            };
        }
        
        private int GetCategoryModuleCount(string category)
        {
            int count = 0;
            foreach (var module in mModules)
            {
                if (module.Category == category) count++;
            }
            return count;
        }
        
        private void SelectModule(int index)
        {
            if (index < 0 || index >= mModules.Count) return;
            
            var selectedModule = mModules[index];
            
            foreach (var kvp in mTocItemMap)
            {
                var item = kvp.Key;
                var arrow = item.Q<Label>("arrow");
                var iconLabel = item.ElementAt(0) as Label;
                var textLabel = item.ElementAt(1) as Label;
                
                if (kvp.Value == index)
                {
                    // 选中状态 - 移动高亮指示器
                    mSelectedTocItem = item;
                    
                    // 延迟一帧获取正确的布局位置
                    item.schedule.Execute(() => MoveHighlightToItem(item)).ExecuteLater(1);
                    
                    // 蓝色选中态：左侧竖条 + 蓝色文字
                    item.style.borderLeftColor = new StyleColor(Theme.AccentBlue);
                    if (arrow != null) arrow.style.color = new StyleColor(Theme.AccentBlue);
                    if (textLabel != null) 
                    {
                        textLabel.style.color = new StyleColor(Theme.AccentBlue);
                        textLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    }
                }
                else
                {
                    item.style.backgroundColor = StyleKeyword.Null;
                    item.style.borderLeftColor = new StyleColor(Color.clear);
                    
                    if (arrow != null) arrow.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.45f));
                    if (textLabel != null) 
                    {
                        textLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.75f));
                        textLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                    }
                }
            }
            
            // 重置滚动位置到顶部
            mContentScrollView.scrollOffset = Vector2.zero;
            
            RenderContent(mModules[index]);
        }
        
        /// <summary>
        /// 将高亮指示器平滑移动到目标项
        /// </summary>
        private void MoveHighlightToItem(VisualElement targetItem)
        {
            if (targetItem == null || mHighlightIndicator == null || mTocItemsContainer == null) return;
            
            // 获取目标项相对于容器的位置
            var targetRect = targetItem.worldBound;
            var containerRect = mTocItemsContainer.worldBound;
            
            // 计算相对位置
            float relativeTop = targetRect.y - containerRect.y + mTocScrollView.scrollOffset.y;
            float relativeLeft = targetRect.x - containerRect.x;
            
            // 设置高亮指示器位置和大小
            mHighlightIndicator.style.top = relativeTop;
            mHighlightIndicator.style.left = relativeLeft;
            mHighlightIndicator.style.width = targetRect.width;
            mHighlightIndicator.style.height = targetRect.height;
            mHighlightIndicator.style.opacity = 1;
        }

        private void RenderContent(DocModule module)
        {
            mContentScrollView.Clear();
            mCurrentHeadings.Clear();
            
            // 居中内容容器
            var centerWrapper = new VisualElement();
            centerWrapper.style.flexGrow = 1;
            centerWrapper.style.alignItems = Align.Center;
            centerWrapper.style.paddingTop = 32;
            centerWrapper.style.paddingBottom = 48;
            centerWrapper.style.paddingRight = 24; // 右侧缓冲区，避免代码块贴边
            
            var content = new VisualElement();
            content.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            content.style.maxWidth = 860; // 稍微减小，给右侧留更多空间
            content.style.paddingLeft = 40;
            content.style.paddingRight = 48; // 增加右侧内边距
            
            // 添加渐入动画
            content.style.opacity = 0;
            content.style.translate = new Translate(0, 10);
            content.style.transitionProperty = new List<StylePropertyName>
            {
                new("opacity"),
                new("translate")
            };
            content.style.transitionDuration = new List<TimeValue>
            {
                new(250, TimeUnit.Millisecond),
                new(250, TimeUnit.Millisecond)
            };
            content.style.transitionTimingFunction = new List<EasingFunction>
            {
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut)
            };
            
            centerWrapper.Add(content);
            mContentScrollView.Add(centerWrapper);
            
            // 面包屑导航
            content.Add(CreateBreadcrumb(module));
            
            // 模块头部（带版本徽章）
            var header = CreateModuleHeader(module);
            content.Add(header);
            mCurrentHeadings.Add((module.Name, header, 1));
            
            // 章节内容
            foreach (var section in module.Sections)
            {
                var sectionElement = CreateSectionElement(section);
                content.Add(sectionElement);
                mCurrentHeadings.Add((section.Title, sectionElement, 2));
            }
            
            // 刷新右侧本页导航
            RefreshOnThisPage();
            
            // 延迟一帧后触发渐入动画
            content.schedule.Execute(() =>
            {
                content.style.opacity = 1;
                content.style.translate = new Translate(0, 0);
            }).ExecuteLater(16);
        }
        
        /// <summary>
        /// 创建面包屑导航
        /// </summary>
        private VisualElement CreateBreadcrumb(DocModule module)
        {
            var breadcrumb = new VisualElement();
            breadcrumb.style.flexDirection = FlexDirection.Row;
            breadcrumb.style.alignItems = Align.Center;
            breadcrumb.style.marginBottom = 16;
            
            var items = new[] { "YokiFrame", module.Category, module.Name };
            
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0)
                {
                    var separator = new Label("›");
                    separator.style.fontSize = 13;
                    separator.style.color = new StyleColor(Theme.TextDim);
                    separator.style.marginLeft = 8;
                    separator.style.marginRight = 8;
                    breadcrumb.Add(separator);
                }
                
                var item = new Label(items[i]);
                item.style.fontSize = 13;
                
                if (i == items.Length - 1)
                {
                    // 当前项
                    item.style.color = new StyleColor(Theme.TextSecondary);
                }
                else
                {
                    // 可点击项
                    item.style.color = new StyleColor(Theme.TextMuted);
                    item.style.transitionProperty = new List<StylePropertyName> { new("color") };
                    item.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };
                    
                    item.RegisterCallback<MouseEnterEvent>(evt =>
                    {
                        item.style.color = new StyleColor(Theme.AccentBlue);
                    });
                    item.RegisterCallback<MouseLeaveEvent>(evt =>
                    {
                        item.style.color = new StyleColor(Theme.TextMuted);
                    });
                }
                
                breadcrumb.Add(item);
            }
            
            return breadcrumb;
        }
        
        private VisualElement CreateModuleHeader(DocModule module)
        {
            var header = new VisualElement();
            header.style.marginBottom = 32;
            header.style.paddingBottom = 24;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(Theme.Border);
            
            // 图标 + 标题行（核心视觉重心）
            var iconTitle = new VisualElement();
            iconTitle.style.flexDirection = FlexDirection.Row;
            iconTitle.style.alignItems = Align.Center;
            
            // 图标背景
            var iconBg = new VisualElement();
            iconBg.style.width = 52;
            iconBg.style.height = 52;
            iconBg.style.borderTopLeftRadius = 12;
            iconBg.style.borderTopRightRadius = 12;
            iconBg.style.borderBottomLeftRadius = 12;
            iconBg.style.borderBottomRightRadius = 12;
            iconBg.style.backgroundColor = new StyleColor(new Color(
                GetCategoryColor(module.Category).r * 0.3f,
                GetCategoryColor(module.Category).g * 0.3f,
                GetCategoryColor(module.Category).b * 0.3f
            ));
            iconBg.style.alignItems = Align.Center;
            iconBg.style.justifyContent = Justify.Center;
            iconBg.style.marginRight = 16;
            
            var icon = new Label(module.Icon);
            icon.style.fontSize = 26;
            iconBg.Add(icon);
            iconTitle.Add(iconBg);
            
            // 标题（第一视觉重心）
            var title = new Label(module.Name);
            title.style.fontSize = 30;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Theme.TextPrimary);
            iconTitle.Add(title);
            
            header.Add(iconTitle);
            
            // 徽章行（放在标题下方）
            var badgeRow = new VisualElement();
            badgeRow.style.flexDirection = FlexDirection.Row;
            badgeRow.style.alignItems = Align.Center;
            badgeRow.style.marginTop = 12;
            badgeRow.style.marginLeft = 68; // 与标题对齐（图标宽度 + margin）
            
            // 分类标签
            if (!string.IsNullOrEmpty(module.Category))
            {
                var categoryBadge = CreateBadge(module.Category, GetCategoryColor(module.Category), true);
                badgeRow.Add(categoryBadge);
            }
            
            // 关键字标签
            if (module.Keywords != null && module.Keywords.Count > 0)
            {
                foreach (var keyword in module.Keywords)
                {
                    var keywordBadge = CreateBadge(keyword, Theme.TextMuted, false);
                    keywordBadge.style.marginLeft = 8;
                    badgeRow.Add(keywordBadge);
                }
            }
            
            header.Add(badgeRow);
            
            // 描述文字
            if (!string.IsNullOrEmpty(module.Description))
            {
                var desc = new Label(module.Description);
                desc.style.fontSize = 15;
                desc.style.marginTop = 16;
                desc.style.marginLeft = 68; // 与标题对齐
                desc.style.color = new StyleColor(Theme.TextMuted);
                desc.style.whiteSpace = WhiteSpace.Normal;
                header.Add(desc);
            }
            
            return header;
        }
        
        /// <summary>
        /// 创建徽章组件
        /// </summary>
        private VisualElement CreateBadge(string text, Color color, bool filled)
        {
            var badge = new Label(text);
            badge.style.fontSize = 11;
            badge.style.paddingLeft = 8;
            badge.style.paddingRight = 8;
            badge.style.paddingTop = 3;
            badge.style.paddingBottom = 3;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            
            if (filled)
            {
                badge.style.color = new StyleColor(color);
                badge.style.backgroundColor = new StyleColor(new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f));
            }
            else
            {
                badge.style.color = new StyleColor(color);
                badge.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.22f));
            }
            
            return badge;
        }
        
        private VisualElement CreateSectionElement(DocSection section)
        {
            var container = new VisualElement();
            container.style.marginBottom = 40;
            
            // 章节标题
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 16;
            
            var marker = new VisualElement();
            marker.style.width = 4;
            marker.style.height = 24;
            marker.style.backgroundColor = new StyleColor(Theme.AccentBlue);
            marker.style.borderTopLeftRadius = 2;
            marker.style.borderTopRightRadius = 2;
            marker.style.borderBottomLeftRadius = 2;
            marker.style.borderBottomRightRadius = 2;
            marker.style.marginRight = 14;
            titleRow.Add(marker);
            
            var title = new Label(section.Title);
            title.style.fontSize = 19;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Theme.TextPrimary);
            titleRow.Add(title);
            
            container.Add(titleRow);
            
            // 章节描述
            if (!string.IsNullOrEmpty(section.Description))
            {
                var desc = new Label(section.Description);
                desc.style.fontSize = 14;
                desc.style.marginBottom = 20;
                desc.style.marginLeft = 18;
                desc.style.color = new StyleColor(Theme.TextMuted);
                desc.style.whiteSpace = WhiteSpace.Normal;
                container.Add(desc);
            }
            
            // 代码示例
            foreach (var example in section.CodeExamples)
            {
                container.Add(CreateCodeExampleElement(example));
            }
            
            return container;
        }
        
        private VisualElement CreateCodeExampleElement(CodeExample example)
        {
            var container = new VisualElement();
            container.style.marginBottom = 20;
            container.style.marginLeft = 18;
            
            // 示例标题栏
            if (!string.IsNullOrEmpty(example.Title))
            {
                var titleBar = new VisualElement();
                titleBar.style.flexDirection = FlexDirection.Row;
                titleBar.style.alignItems = Align.Center;
                titleBar.style.marginBottom = 8;
                
                var dot = new Label("●");
                dot.style.fontSize = 8;
                dot.style.color = new StyleColor(Theme.AccentGreen);
                dot.style.marginRight = 8;
                titleBar.Add(dot);
                
                var title = new Label(example.Title);
                title.style.fontSize = 13;
                title.style.color = new StyleColor(Theme.TextSecondary);
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                titleBar.Add(title);
                
                container.Add(titleBar);
            }
            
            // 代码块容器
            var codeContainer = new VisualElement();
            codeContainer.style.borderTopLeftRadius = 8;
            codeContainer.style.borderTopRightRadius = 8;
            codeContainer.style.borderBottomLeftRadius = 8;
            codeContainer.style.borderBottomRightRadius = 8;
            codeContainer.style.borderLeftWidth = 1;
            codeContainer.style.borderRightWidth = 1;
            codeContainer.style.borderTopWidth = 1;
            codeContainer.style.borderBottomWidth = 1;
            codeContainer.style.borderLeftColor = new StyleColor(Theme.Border);
            codeContainer.style.borderRightColor = new StyleColor(Theme.Border);
            codeContainer.style.borderTopColor = new StyleColor(Theme.Border);
            codeContainer.style.borderBottomColor = new StyleColor(Theme.Border);
            codeContainer.style.overflow = Overflow.Hidden;
            
            // 代码块头部（带复制按钮）
            var codeHeader = new VisualElement();
            codeHeader.style.flexDirection = FlexDirection.Row;
            codeHeader.style.justifyContent = Justify.SpaceBetween;
            codeHeader.style.alignItems = Align.Center;
            codeHeader.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.08f));
            codeHeader.style.paddingLeft = 16;
            codeHeader.style.paddingRight = 8;
            codeHeader.style.paddingTop = 6;
            codeHeader.style.paddingBottom = 6;
            codeHeader.style.borderBottomWidth = 1;
            codeHeader.style.borderBottomColor = new StyleColor(Theme.Border);
            
            var langLabel = new Label("C#");
            langLabel.style.fontSize = 11;
            langLabel.style.color = new StyleColor(Theme.TextDim);
            codeHeader.Add(langLabel);
            
            // 复制按钮
            var copyBtn = new Button(() => CopyToClipboard(example.Code));
            copyBtn.text = "📋 复制";
            copyBtn.style.fontSize = 11;
            copyBtn.style.paddingLeft = 8;
            copyBtn.style.paddingRight = 8;
            copyBtn.style.paddingTop = 4;
            copyBtn.style.paddingBottom = 4;
            copyBtn.style.borderTopLeftRadius = 4;
            copyBtn.style.borderTopRightRadius = 4;
            copyBtn.style.borderBottomLeftRadius = 4;
            copyBtn.style.borderBottomRightRadius = 4;
            copyBtn.style.backgroundColor = new StyleColor(Theme.BgHover);
            copyBtn.style.borderLeftWidth = 0;
            copyBtn.style.borderRightWidth = 0;
            copyBtn.style.borderTopWidth = 0;
            copyBtn.style.borderBottomWidth = 0;
            copyBtn.style.color = new StyleColor(Theme.TextMuted);
            codeHeader.Add(copyBtn);
            
            codeContainer.Add(codeHeader);
            
            // 代码内容 - 双层叠加实现：语法高亮 + 可选中复制
            var codeBlock = new VisualElement();
            codeBlock.style.backgroundColor = new StyleColor(Theme.BgCode);
            codeBlock.style.paddingLeft = 16;
            codeBlock.style.paddingRight = 16;
            codeBlock.style.paddingTop = 14;
            codeBlock.style.paddingBottom = 14;
            codeBlock.style.position = Position.Relative;
            
            // 底层：可选中的 TextField（文字透明，只用于选中复制）
            var codeTextField = new TextField();
            codeTextField.multiline = true;
            codeTextField.isReadOnly = true;
            codeTextField.value = example.Code;
            codeTextField.style.position = Position.Absolute;
            codeTextField.style.left = 16;
            codeTextField.style.right = 16;
            codeTextField.style.top = 14;
            codeTextField.style.bottom = 14;
            
            // 移除 TextField 默认样式
            codeTextField.style.marginLeft = 0;
            codeTextField.style.marginRight = 0;
            codeTextField.style.marginTop = 0;
            codeTextField.style.marginBottom = 0;
            codeTextField.style.paddingLeft = 0;
            codeTextField.style.paddingRight = 0;
            codeTextField.style.paddingTop = 0;
            codeTextField.style.paddingBottom = 0;
            codeTextField.style.backgroundColor = new StyleColor(Color.clear);
            codeTextField.style.borderLeftWidth = 0;
            codeTextField.style.borderRightWidth = 0;
            codeTextField.style.borderTopWidth = 0;
            codeTextField.style.borderBottomWidth = 0;
            
            // 样式化内部输入区域 - 文字透明但选中时可见
            var textInput = codeTextField.Q<VisualElement>("unity-text-input");
            if (textInput != null)
            {
                textInput.style.backgroundColor = new StyleColor(Color.clear);
                textInput.style.borderLeftWidth = 0;
                textInput.style.borderRightWidth = 0;
                textInput.style.borderTopWidth = 0;
                textInput.style.borderBottomWidth = 0;
                textInput.style.paddingLeft = 0;
                textInput.style.paddingRight = 0;
                textInput.style.paddingTop = 0;
                textInput.style.paddingBottom = 0;
                textInput.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.01f)); // 几乎透明
            }
            
            codeTextField.style.fontSize = 13;
            codeTextField.style.whiteSpace = WhiteSpace.Pre;
            codeTextField.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.01f)); // 几乎透明
            
            codeBlock.Add(codeTextField);
            
            // 顶层：语法高亮的 Label（不可交互，仅显示）
            var highlightedCode = CSharpSyntaxHighlighter.Highlight(example.Code);
            var codeLabel = new Label();
            codeLabel.enableRichText = true;
            codeLabel.text = highlightedCode;
            codeLabel.style.fontSize = 13;
            codeLabel.style.whiteSpace = WhiteSpace.Pre;
            codeLabel.pickingMode = PickingMode.Ignore; // 不拦截鼠标事件，让底层 TextField 接收
            codeBlock.Add(codeLabel);
            
            codeContainer.Add(codeBlock);
            container.Add(codeContainer);
            
            // 说明提示框 - 更亮的背景色
            if (!string.IsNullOrEmpty(example.Explanation))
            {
                var explanationBox = new VisualElement();
                explanationBox.style.flexDirection = FlexDirection.Row;
                explanationBox.style.marginTop = 12;
                explanationBox.style.paddingLeft = 14;
                explanationBox.style.paddingRight = 14;
                explanationBox.style.paddingTop = 12;
                explanationBox.style.paddingBottom = 12;
                explanationBox.style.backgroundColor = new StyleColor(new Color(0.22f, 0.18f, 0.08f)); // 更亮的黄色背景
                explanationBox.style.borderTopLeftRadius = 6;
                explanationBox.style.borderTopRightRadius = 6;
                explanationBox.style.borderBottomLeftRadius = 6;
                explanationBox.style.borderBottomRightRadius = 6;
                explanationBox.style.borderLeftWidth = 3;
                explanationBox.style.borderLeftColor = new StyleColor(new Color(0.95f, 0.75f, 0.2f)); // 更亮的黄色边框
                
                var infoIcon = new Label("💡");
                infoIcon.style.fontSize = 17;
                infoIcon.style.marginRight = 12;
                explanationBox.Add(infoIcon);
                
                var explanation = new Label(example.Explanation);
                explanation.style.fontSize = 14;
                explanation.style.color = new StyleColor(new Color(0.9f, 0.85f, 0.7f)); // 更亮的文字
                explanation.style.whiteSpace = WhiteSpace.Normal;
                explanation.style.flexShrink = 1;
                explanationBox.Add(explanation);
                
                container.Add(explanationBox);
            }
            
            return container;
        }
        
        private void CopyToClipboard(string text)
        {
            EditorGUIUtility.systemCopyBuffer = text;
            Debug.Log("[YokiFrame] 代码已复制到剪贴板");
        }

        #region Documentation Data
        
        private void InitializeDocumentation()
        {
            mModules.Clear();
            
            // Architecture
            mModules.Add(CreateArchitectureDoc());
            
            // Core Kit
            mModules.Add(CreateEventKitDoc());
            mModules.Add(CreateFsmKitDoc());
            mModules.Add(CreatePoolKitDoc());
            mModules.Add(CreateSingletonKitDoc());
            mModules.Add(CreateResKitDoc());
            mModules.Add(CreateLogKitDoc());
            mModules.Add(CreateCodeGenKitDoc());
            mModules.Add(CreateFluentApiDoc());
            mModules.Add(CreateToolClassDoc());
            
            // Tools
            mModules.Add(CreateUIKitDoc());
            mModules.Add(CreateActionKitDoc());
            mModules.Add(CreateAudioKitDoc());
            mModules.Add(CreateSaveKitDoc());
            mModules.Add(CreateTableKitDoc());
            mModules.Add(CreateBuffKitDoc());
            mModules.Add(CreateLocalizationKitDoc());
            mModules.Add(CreateSceneKitDoc());
        }
        
        #endregion
        
        #region Data Structures
        
        private class DocModule
        {
            public string Name;
            public string Icon;
            public string Category;
            public string Description;
            public List<string> Keywords = new();
            public List<DocSection> Sections = new();
        }
        
        private class DocSection
        {
            public string Title;
            public string Description;
            public List<CodeExample> CodeExamples = new();
        }
        
        private class CodeExample
        {
            public string Title;
            public string Code;
            public string Explanation;
        }
        
        #endregion
    }
}
#endif
