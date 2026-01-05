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
        public override int Priority => 0;
        
        private ScrollView mTocScrollView;
        private ScrollView mContentScrollView;
        private readonly List<DocModule> mModules = new();
        private readonly Dictionary<VisualElement, int> mTocItemMap = new();
        private VisualElement mSelectedTocItem;
        
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
            
            // 分类颜色（更鲜艳的主题色）
            public static readonly Color CategoryCore = new(0.4f, 0.7f, 1f);
            public static readonly Color CategoryKit = new(0.3f, 0.85f, 0.5f);
            public static readonly Color CategoryTools = new(1f, 0.65f, 0.2f);
            
            // 分类背景渐变色
            public static readonly Color CategoryCoreBg = new(0.15f, 0.25f, 0.35f);
            public static readonly Color CategoryKitBg = new(0.15f, 0.3f, 0.2f);
            public static readonly Color CategoryToolsBg = new(0.3f, 0.22f, 0.15f);
        }
        
        protected override void BuildUI(VisualElement root)
        {
            InitializeDocumentation();
            
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexGrow = 1;
            root.Add(container);
            
            container.Add(CreateTocPanel());
            
            mContentScrollView = new ScrollView();
            mContentScrollView.style.flexGrow = 1;
            mContentScrollView.style.backgroundColor = new StyleColor(Theme.BgPrimary);
            container.Add(mContentScrollView);
            
            if (mModules.Count > 0) SelectModule(0);
        }
        
        private VisualElement CreateTocPanel()
        {
            var panel = new VisualElement();
            panel.style.width = 260;
            panel.style.minWidth = 240;
            panel.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.1f));
            panel.style.borderRightWidth = 1;
            panel.style.borderRightColor = new StyleColor(new Color(0.2f, 0.25f, 0.35f, 0.3f));
            
            // 标题区域 - 渐变背景
            var headerBox = new VisualElement();
            headerBox.style.paddingLeft = 20;
            headerBox.style.paddingRight = 20;
            headerBox.style.paddingTop = 28;
            headerBox.style.paddingBottom = 24;
            headerBox.style.backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.08f));
            
            // 添加顶部装饰线
            var topAccent = new VisualElement();
            topAccent.style.height = 3;
            topAccent.style.backgroundColor = new StyleColor(new Color(0.4f, 0.7f, 1f, 0.8f));
            headerBox.Add(topAccent);
            
            var logoRow = new VisualElement();
            logoRow.style.flexDirection = FlexDirection.Row;
            logoRow.style.alignItems = Align.Center;
            logoRow.style.marginTop = 16;
            
            // Logo 容器带发光效果
            var logoContainer = new VisualElement();
            logoContainer.style.width = 42;
            logoContainer.style.height = 42;
            logoContainer.style.borderTopLeftRadius = 10;
            logoContainer.style.borderTopRightRadius = 10;
            logoContainer.style.borderBottomLeftRadius = 10;
            logoContainer.style.borderBottomRightRadius = 10;
            logoContainer.style.backgroundColor = new StyleColor(new Color(0.4f, 0.7f, 1f, 0.15f));
            logoContainer.style.alignItems = Align.Center;
            logoContainer.style.justifyContent = Justify.Center;
            logoContainer.style.marginRight = 12;
            logoContainer.style.borderLeftWidth = 2;
            logoContainer.style.borderLeftColor = new StyleColor(new Color(0.4f, 0.7f, 1f, 0.6f));
            
            var logo = new Label("📚");
            logo.style.fontSize = 24;
            logoContainer.Add(logo);
            logoRow.Add(logoContainer);
            
            var titleBox = new VisualElement();
            var title = new Label("YokiFrame");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(new Color(0.95f, 0.97f, 1f));
            titleBox.Add(title);
            
            var subtitle = new Label("Documentation");
            subtitle.style.fontSize = 10;
            subtitle.style.color = new StyleColor(new Color(0.5f, 0.75f, 1f, 0.8f));
            subtitle.style.marginTop = 3;
            subtitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            subtitle.style.letterSpacing = 1;
            titleBox.Add(subtitle);
            
            logoRow.Add(titleBox);
            headerBox.Add(logoRow);
            
            // 版本信息带徽章样式
            var versionRow = new VisualElement();
            versionRow.style.flexDirection = FlexDirection.Row;
            versionRow.style.marginTop = 16;
            versionRow.style.alignItems = Align.Center;
            
            var versionBadge = new Label("v1.0.0");
            versionBadge.style.fontSize = 9;
            versionBadge.style.color = new StyleColor(new Color(0.3f, 0.9f, 0.6f));
            versionBadge.style.backgroundColor = new StyleColor(new Color(0.1f, 0.3f, 0.2f, 0.5f));
            versionBadge.style.paddingLeft = 8;
            versionBadge.style.paddingRight = 8;
            versionBadge.style.paddingTop = 3;
            versionBadge.style.paddingBottom = 3;
            versionBadge.style.borderTopLeftRadius = 10;
            versionBadge.style.borderTopRightRadius = 10;
            versionBadge.style.borderBottomLeftRadius = 10;
            versionBadge.style.borderBottomRightRadius = 10;
            versionBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            versionRow.Add(versionBadge);
            
            var separator = new Label("•");
            separator.style.fontSize = 10;
            separator.style.color = new StyleColor(new Color(0.3f, 0.3f, 0.4f));
            separator.style.marginLeft = 8;
            separator.style.marginRight = 8;
            versionRow.Add(separator);
            
            var apiLabel = new Label("API Reference");
            apiLabel.style.fontSize = 9;
            apiLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.6f));
            versionRow.Add(apiLabel);
            
            headerBox.Add(versionRow);
            panel.Add(headerBox);
            
            mTocScrollView = new ScrollView();
            mTocScrollView.style.flexGrow = 1;
            mTocScrollView.style.paddingTop = 16;
            mTocScrollView.style.paddingBottom = 16;
            panel.Add(mTocScrollView);
            
            RefreshToc();
            return panel;
        }
        
        private void RefreshToc()
        {
            mTocScrollView.Clear();
            mTocItemMap.Clear();
            
            string currentCategory = null;
            VisualElement categoryGroup = null;
            
            for (int i = 0; i < mModules.Count; i++)
            {
                var module = mModules[i];
                var moduleIndex = i;
                
                if (module.Category != currentCategory)
                {
                    currentCategory = module.Category;
                    
                    // 创建分类组容器（带炫酷渐变和发光效果）
                    categoryGroup = new VisualElement();
                    categoryGroup.style.marginTop = i == 0 ? 0 : 16;
                    categoryGroup.style.marginLeft = 10;
                    categoryGroup.style.marginRight = 10;
                    categoryGroup.style.marginBottom = 4;
                    categoryGroup.style.borderTopLeftRadius = 12;
                    categoryGroup.style.borderTopRightRadius = 12;
                    categoryGroup.style.borderBottomLeftRadius = 12;
                    categoryGroup.style.borderBottomRightRadius = 12;
                    categoryGroup.style.overflow = Overflow.Hidden;
                    
                    var categoryColor = GetCategoryColor(currentCategory);
                    var categoryBgColor = GetCategoryBgColor(currentCategory);
                    
                    // 背景渐变层
                    categoryGroup.style.backgroundColor = new StyleColor(categoryBgColor);
                    
                    // 左侧发光边框
                    categoryGroup.style.borderLeftWidth = 3;
                    categoryGroup.style.borderLeftColor = new StyleColor(categoryColor);
                    
                    // 顶部装饰条
                    var topBar = new VisualElement();
                    topBar.style.height = 2;
                    topBar.style.backgroundColor = new StyleColor(new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.4f));
                    categoryGroup.Add(topBar);
                    
                    // 分类标题栏
                    var categoryHeader = new VisualElement();
                    categoryHeader.style.flexDirection = FlexDirection.Row;
                    categoryHeader.style.alignItems = Align.Center;
                    categoryHeader.style.paddingLeft = 14;
                    categoryHeader.style.paddingRight = 12;
                    categoryHeader.style.paddingTop = 12;
                    categoryHeader.style.paddingBottom = 12;
                    categoryHeader.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.25f));
                    
                    // 分类图标容器（带发光背景）
                    var iconBg = new VisualElement();
                    iconBg.style.width = 32;
                    iconBg.style.height = 32;
                    iconBg.style.borderTopLeftRadius = 8;
                    iconBg.style.borderTopRightRadius = 8;
                    iconBg.style.borderBottomLeftRadius = 8;
                    iconBg.style.borderBottomRightRadius = 8;
                    iconBg.style.backgroundColor = new StyleColor(new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.2f));
                    iconBg.style.alignItems = Align.Center;
                    iconBg.style.justifyContent = Justify.Center;
                    iconBg.style.marginRight = 10;
                    iconBg.style.borderLeftWidth = 2;
                    iconBg.style.borderLeftColor = new StyleColor(categoryColor);
                    
                    var categoryIcon = new Label(GetCategoryIcon(currentCategory));
                    categoryIcon.style.fontSize = 16;
                    iconBg.Add(categoryIcon);
                    categoryHeader.Add(iconBg);
                    
                    // 分类标签
                    var categoryLabel = new Label(currentCategory);
                    categoryLabel.style.fontSize = 11;
                    categoryLabel.style.color = new StyleColor(categoryColor);
                    categoryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    categoryLabel.style.flexGrow = 1;
                    categoryLabel.style.letterSpacing = 0.5f;
                    categoryHeader.Add(categoryLabel);
                    
                    // 分类徽章（显示数量）- 更炫酷的设计
                    var countBadge = new Label(GetCategoryModuleCount(currentCategory).ToString());
                    countBadge.style.fontSize = 10;
                    countBadge.style.color = new StyleColor(new Color(0.05f, 0.05f, 0.08f));
                    countBadge.style.backgroundColor = new StyleColor(categoryColor);
                    countBadge.style.paddingLeft = 8;
                    countBadge.style.paddingRight = 8;
                    countBadge.style.paddingTop = 4;
                    countBadge.style.paddingBottom = 4;
                    countBadge.style.borderTopLeftRadius = 10;
                    countBadge.style.borderTopRightRadius = 10;
                    countBadge.style.borderBottomLeftRadius = 10;
                    countBadge.style.borderBottomRightRadius = 10;
                    countBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
                    categoryHeader.Add(countBadge);
                    
                    categoryGroup.Add(categoryHeader);
                    mTocScrollView.Add(categoryGroup);
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
            item.style.paddingLeft = 12;
            item.style.paddingRight = 10;
            item.style.paddingTop = 8;
            item.style.paddingBottom = 8;
            item.style.marginLeft = 6;
            item.style.marginRight = 6;
            item.style.marginTop = 3;
            item.style.marginBottom = 3;
            item.style.borderTopLeftRadius = 8;
            item.style.borderTopRightRadius = 8;
            item.style.borderBottomLeftRadius = 8;
            item.style.borderBottomRightRadius = 8;
            item.style.borderLeftWidth = 0;
            item.style.borderLeftColor = new StyleColor(Color.clear);
            
            var categoryColor = GetCategoryColor(module.Category);
            
            // 图标容器（带发光背景和边框）
            var iconContainer = new VisualElement();
            iconContainer.style.width = 32;
            iconContainer.style.height = 32;
            iconContainer.style.borderTopLeftRadius = 8;
            iconContainer.style.borderTopRightRadius = 8;
            iconContainer.style.borderBottomLeftRadius = 8;
            iconContainer.style.borderBottomRightRadius = 8;
            iconContainer.style.backgroundColor = new StyleColor(new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.12f));
            iconContainer.style.alignItems = Align.Center;
            iconContainer.style.justifyContent = Justify.Center;
            iconContainer.style.marginRight = 10;
            iconContainer.style.borderLeftWidth = 2;
            iconContainer.style.borderLeftColor = new StyleColor(new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.4f));
            
            var icon = new Label(module.Icon);
            icon.style.fontSize = 16;
            iconContainer.Add(icon);
            item.Add(iconContainer);
            
            var label = new Label(module.Name);
            label.style.fontSize = 12;
            label.style.color = new StyleColor(new Color(0.85f, 0.85f, 0.9f));
            label.style.flexGrow = 1;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            item.Add(label);
            
            // 箭头指示器（更现代的设计）
            var arrowContainer = new VisualElement();
            arrowContainer.style.width = 20;
            arrowContainer.style.height = 20;
            arrowContainer.style.borderTopLeftRadius = 4;
            arrowContainer.style.borderTopRightRadius = 4;
            arrowContainer.style.borderBottomLeftRadius = 4;
            arrowContainer.style.borderBottomRightRadius = 4;
            arrowContainer.style.alignItems = Align.Center;
            arrowContainer.style.justifyContent = Justify.Center;
            arrowContainer.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.05f));
            
            var arrow = new Label("▸");
            arrow.style.fontSize = 11;
            arrow.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.6f));
            arrow.name = "arrow";
            arrowContainer.Add(arrow);
            item.Add(arrowContainer);
            
            mTocItemMap[item] = index;
            
            item.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (item != mSelectedTocItem)
                {
                    item.style.backgroundColor = new StyleColor(new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.15f));
                    item.style.borderLeftWidth = 3;
                    item.style.borderLeftColor = new StyleColor(new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.6f));
                    
                    var arrowLabel = item.Q<Label>("arrow");
                    if (arrowLabel != null) arrowLabel.style.color = new StyleColor(categoryColor);
                    
                    var iconCont = item.ElementAt(0);
                    iconCont.style.backgroundColor = new StyleColor(new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.25f));
                    iconCont.style.borderLeftColor = new StyleColor(categoryColor);
                }
            });
            item.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (item != mSelectedTocItem)
                {
                    item.style.backgroundColor = StyleKeyword.Null;
                    item.style.borderLeftWidth = 0;
                    item.style.borderLeftColor = new StyleColor(Color.clear);
                    
                    var arrowLabel = item.Q<Label>("arrow");
                    if (arrowLabel != null) arrowLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.6f));
                    
                    var iconCont = item.ElementAt(0);
                    iconCont.style.backgroundColor = new StyleColor(new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.12f));
                    iconCont.style.borderLeftColor = new StyleColor(new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.4f));
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
            var categoryColor = GetCategoryColor(selectedModule.Category);
            
            foreach (var kvp in mTocItemMap)
            {
                var item = kvp.Key;
                var moduleIndex = kvp.Value;
                var module = mModules[moduleIndex];
                var itemCategoryColor = GetCategoryColor(module.Category);
                var arrow = item.Q<Label>("arrow");
                var iconCont = item.ElementAt(0);
                
                if (kvp.Value == index)
                {
                    // 选中状态 - 更鲜艳的发光效果
                    item.style.backgroundColor = new StyleColor(new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.3f));
                    item.style.borderLeftWidth = 3;
                    item.style.borderLeftColor = new StyleColor(categoryColor);
                    
                    if (arrow != null) arrow.style.color = new StyleColor(new Color(0.05f, 0.05f, 0.08f));
                    
                    // 箭头容器背景
                    var arrowContainer = arrow.parent;
                    arrowContainer.style.backgroundColor = new StyleColor(categoryColor);
                    
                    // 图标容器发光
                    iconCont.style.backgroundColor = new StyleColor(new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.4f));
                    iconCont.style.borderLeftColor = new StyleColor(categoryColor);
                    
                    mSelectedTocItem = item;
                }
                else
                {
                    item.style.backgroundColor = StyleKeyword.Null;
                    item.style.borderLeftWidth = 0;
                    item.style.borderLeftColor = new StyleColor(Color.clear);
                    
                    if (arrow != null) arrow.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.6f));
                    
                    var arrowContainer = arrow.parent;
                    arrowContainer.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.05f));
                    
                    iconCont.style.backgroundColor = new StyleColor(new Color(itemCategoryColor.r, itemCategoryColor.g, itemCategoryColor.b, 0.12f));
                    iconCont.style.borderLeftColor = new StyleColor(new Color(itemCategoryColor.r, itemCategoryColor.g, itemCategoryColor.b, 0.4f));
                    iconCont.style.borderLeftColor = new StyleColor(new Color(itemCategoryColor.r, itemCategoryColor.g, itemCategoryColor.b, 0.4f));
                    if (arrow != null) arrow.style.color = new StyleColor(Theme.TextDim);
                }
            }
            
            // 重置滚动位置到顶部
            mContentScrollView.scrollOffset = Vector2.zero;
            
            RenderContent(mModules[index]);
        }

        private void RenderContent(DocModule module)
        {
            mContentScrollView.Clear();
            
            var content = new VisualElement();
            content.style.paddingLeft = 40;
            content.style.paddingRight = 40;
            content.style.paddingTop = 32;
            content.style.paddingBottom = 48;
            content.style.maxWidth = 920;
            mContentScrollView.Add(content);
            
            // 模块头部
            content.Add(CreateModuleHeader(module));
            
            // 章节内容
            foreach (var section in module.Sections)
            {
                content.Add(CreateSectionElement(section));
            }
        }
        
        private VisualElement CreateModuleHeader(DocModule module)
        {
            var header = new VisualElement();
            header.style.marginBottom = 32;
            header.style.paddingBottom = 24;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(Theme.Border);
            
            var iconTitle = new VisualElement();
            iconTitle.style.flexDirection = FlexDirection.Row;
            iconTitle.style.alignItems = Align.Center;
            
            // 图标背景
            var iconBg = new VisualElement();
            iconBg.style.width = 56;
            iconBg.style.height = 56;
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
            icon.style.fontSize = 28;
            iconBg.Add(icon);
            iconTitle.Add(iconBg);
            
            var titleBox = new VisualElement();
            
            // 分类标签
            if (!string.IsNullOrEmpty(module.Category))
            {
                var badge = new Label(module.Category);
                badge.style.fontSize = 10;
                badge.style.color = new StyleColor(GetCategoryColor(module.Category));
                badge.style.backgroundColor = new StyleColor(new Color(
                    GetCategoryColor(module.Category).r * 0.2f,
                    GetCategoryColor(module.Category).g * 0.2f,
                    GetCategoryColor(module.Category).b * 0.2f
                ));
                badge.style.paddingLeft = 8;
                badge.style.paddingRight = 8;
                badge.style.paddingTop = 3;
                badge.style.paddingBottom = 3;
                badge.style.borderTopLeftRadius = 4;
                badge.style.borderTopRightRadius = 4;
                badge.style.borderBottomLeftRadius = 4;
                badge.style.borderBottomRightRadius = 4;
                badge.style.marginBottom = 6;
                badge.style.alignSelf = Align.FlexStart;
                badge.style.unityFontStyleAndWeight = FontStyle.Bold;
                titleBox.Add(badge);
            }
            
            var title = new Label(module.Name);
            title.style.fontSize = 28;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Theme.TextPrimary);
            titleBox.Add(title);
            
            iconTitle.Add(titleBox);
            header.Add(iconTitle);
            
            if (!string.IsNullOrEmpty(module.Description))
            {
                var desc = new Label(module.Description);
                desc.style.fontSize = 14;
                desc.style.marginTop = 16;
                desc.style.color = new StyleColor(Theme.TextMuted);
                desc.style.whiteSpace = WhiteSpace.Normal;
                header.Add(desc);
            }
            
            return header;
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
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Theme.TextPrimary);
            titleRow.Add(title);
            
            container.Add(titleRow);
            
            // 章节描述
            if (!string.IsNullOrEmpty(section.Description))
            {
                var desc = new Label(section.Description);
                desc.style.fontSize = 13;
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
                title.style.fontSize = 12;
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
            langLabel.style.fontSize = 10;
            langLabel.style.color = new StyleColor(Theme.TextDim);
            codeHeader.Add(langLabel);
            
            // 复制按钮
            var copyBtn = new Button(() => CopyToClipboard(example.Code));
            copyBtn.text = "📋 复制";
            copyBtn.style.fontSize = 10;
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
            
            // 代码内容
            var codeBlock = new VisualElement();
            codeBlock.style.backgroundColor = new StyleColor(Theme.BgCode);
            codeBlock.style.paddingLeft = 16;
            codeBlock.style.paddingRight = 16;
            codeBlock.style.paddingTop = 14;
            codeBlock.style.paddingBottom = 14;
            
            var highlightedCode = CSharpSyntaxHighlighter.Highlight(example.Code);
            
            var codeLabel = new Label();
            codeLabel.enableRichText = true;
            codeLabel.text = highlightedCode;
            codeLabel.style.fontSize = 12;
            codeLabel.style.whiteSpace = WhiteSpace.Pre;
            codeBlock.Add(codeLabel);
            
            codeContainer.Add(codeBlock);
            container.Add(codeContainer);
            
            // 说明提示框
            if (!string.IsNullOrEmpty(example.Explanation))
            {
                var explanationBox = new VisualElement();
                explanationBox.style.flexDirection = FlexDirection.Row;
                explanationBox.style.marginTop = 12;
                explanationBox.style.paddingLeft = 14;
                explanationBox.style.paddingRight = 14;
                explanationBox.style.paddingTop = 10;
                explanationBox.style.paddingBottom = 10;
                explanationBox.style.backgroundColor = new StyleColor(new Color(
                    Theme.AccentYellow.r * 0.15f,
                    Theme.AccentYellow.g * 0.15f,
                    Theme.AccentYellow.b * 0.15f
                ));
                explanationBox.style.borderTopLeftRadius = 6;
                explanationBox.style.borderTopRightRadius = 6;
                explanationBox.style.borderBottomLeftRadius = 6;
                explanationBox.style.borderBottomRightRadius = 6;
                explanationBox.style.borderLeftWidth = 3;
                explanationBox.style.borderLeftColor = new StyleColor(Theme.AccentYellow);
                
                var infoIcon = new Label("💡");
                infoIcon.style.fontSize = 14;
                infoIcon.style.marginRight = 10;
                explanationBox.Add(infoIcon);
                
                var explanation = new Label(example.Explanation);
                explanation.style.fontSize = 12;
                explanation.style.color = new StyleColor(Theme.TextSecondary);
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
        }
        
        #endregion
        
        #region Data Structures
        
        private class DocModule
        {
            public string Name;
            public string Icon;
            public string Category;
            public string Description;
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
