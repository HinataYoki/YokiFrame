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
                    
                    // 创建分类组容器（扁平化风格）
                    categoryGroup = new VisualElement();
                    categoryGroup.style.marginTop = i == 0 ? 0 : 12;
                    categoryGroup.style.marginLeft = 10;
                    categoryGroup.style.marginRight = 10;
                    categoryGroup.style.marginBottom = 4;
                    categoryGroup.style.borderTopLeftRadius = 8;
                    categoryGroup.style.borderTopRightRadius = 8;
                    categoryGroup.style.borderBottomLeftRadius = 8;
                    categoryGroup.style.borderBottomRightRadius = 8;
                    categoryGroup.style.overflow = Overflow.Hidden;
                    
                    var categoryColor = GetCategoryColor(currentCategory);
                    var categoryBgColor = GetCategoryBgColor(currentCategory);
                    
                    // 扁平背景
                    categoryGroup.style.backgroundColor = new StyleColor(categoryBgColor);
                    
                    // 细微左侧边框
                    categoryGroup.style.borderLeftWidth = 2;
                    categoryGroup.style.borderLeftColor = new StyleColor(new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.4f));
                    
                    // 分类标题栏
                    var categoryHeader = new VisualElement();
                    categoryHeader.style.flexDirection = FlexDirection.Row;
                    categoryHeader.style.alignItems = Align.Center;
                    categoryHeader.style.paddingLeft = 12;
                    categoryHeader.style.paddingRight = 12;
                    categoryHeader.style.paddingTop = 10;
                    categoryHeader.style.paddingBottom = 10;
                    
                    // 分类图标
                    var categoryIcon = new Label(GetCategoryIcon(currentCategory));
                    categoryIcon.style.fontSize = 14;
                    categoryIcon.style.marginRight = 8;
                    categoryHeader.Add(categoryIcon);
                    
                    // 分类标签
                    var categoryLabel = new Label(currentCategory);
                    categoryLabel.style.fontSize = 11;
                    categoryLabel.style.color = new StyleColor(new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.9f));
                    categoryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    categoryLabel.style.flexGrow = 1;
                    categoryLabel.style.letterSpacing = 0.5f;
                    categoryHeader.Add(categoryLabel);
                    
                    // 分类徽章（显示数量）
                    var countBadge = new Label(GetCategoryModuleCount(currentCategory).ToString());
                    countBadge.style.fontSize = 10;
                    countBadge.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.75f));
                    countBadge.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.22f));
                    countBadge.style.paddingLeft = 7;
                    countBadge.style.paddingRight = 7;
                    countBadge.style.paddingTop = 3;
                    countBadge.style.paddingBottom = 3;
                    countBadge.style.borderTopLeftRadius = 8;
                    countBadge.style.borderTopRightRadius = 8;
                    countBadge.style.borderBottomLeftRadius = 8;
                    countBadge.style.borderBottomRightRadius = 8;
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
            item.style.marginLeft = 4;
            item.style.marginRight = 4;
            item.style.marginTop = 2;
            item.style.marginBottom = 2;
            item.style.borderTopLeftRadius = 6;
            item.style.borderTopRightRadius = 6;
            item.style.borderBottomLeftRadius = 6;
            item.style.borderBottomRightRadius = 6;
            
            var categoryColor = GetCategoryColor(module.Category);
            
            // 简化的图标容器
            var iconContainer = new VisualElement();
            iconContainer.style.width = 28;
            iconContainer.style.height = 28;
            iconContainer.style.borderTopLeftRadius = 6;
            iconContainer.style.borderTopRightRadius = 6;
            iconContainer.style.borderBottomLeftRadius = 6;
            iconContainer.style.borderBottomRightRadius = 6;
            iconContainer.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f));
            iconContainer.style.alignItems = Align.Center;
            iconContainer.style.justifyContent = Justify.Center;
            iconContainer.style.marginRight = 10;
            
            var icon = new Label(module.Icon);
            icon.style.fontSize = 14;
            iconContainer.Add(icon);
            item.Add(iconContainer);
            
            var label = new Label(module.Name);
            label.style.fontSize = 12;
            label.style.color = new StyleColor(new Color(0.75f, 0.75f, 0.8f));
            label.style.flexGrow = 1;
            item.Add(label);
            
            // 简化的箭头指示器
            var arrow = new Label("›");
            arrow.style.fontSize = 14;
            arrow.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.45f));
            arrow.name = "arrow";
            item.Add(arrow);
            
            mTocItemMap[item] = index;
            
            item.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (item != mSelectedTocItem)
                {
                    item.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.25f));
                    
                    var arrowLabel = item.Q<Label>("arrow");
                    if (arrowLabel != null) arrowLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.65f));
                    
                    var iconCont = item.ElementAt(0);
                    iconCont.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.25f));
                }
            });
            item.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (item != mSelectedTocItem)
                {
                    item.style.backgroundColor = StyleKeyword.Null;
                    
                    var arrowLabel = item.Q<Label>("arrow");
                    if (arrowLabel != null) arrowLabel.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.45f));
                    
                    var iconCont = item.ElementAt(0);
                    iconCont.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f));
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
                var arrow = item.Q<Label>("arrow");
                var iconCont = item.ElementAt(0);
                
                if (kvp.Value == index)
                {
                    // 选中状态 - 扁平化风格
                    item.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.28f));
                    
                    if (arrow != null) arrow.style.color = new StyleColor(new Color(0.85f, 0.85f, 0.9f));
                    
                    // 图标容器
                    iconCont.style.backgroundColor = new StyleColor(new Color(0.28f, 0.28f, 0.32f));
                    
                    // 文字高亮
                    var label = item.ElementAt(1) as Label;
                    if (label != null) label.style.color = new StyleColor(new Color(0.95f, 0.95f, 0.98f));
                    
                    mSelectedTocItem = item;
                }
                else
                {
                    item.style.backgroundColor = StyleKeyword.Null;
                    
                    if (arrow != null) arrow.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.45f));
                    
                    iconCont.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f));
                    
                    // 文字恢复
                    var label = item.ElementAt(1) as Label;
                    if (label != null) label.style.color = new StyleColor(new Color(0.75f, 0.75f, 0.8f));
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
            mModules.Add(CreateTableKitDoc());
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
