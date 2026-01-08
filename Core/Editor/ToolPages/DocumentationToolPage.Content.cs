#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 文档页面 - 内容渲染
    /// </summary>
    public partial class DocumentationToolPage
    {
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
            centerWrapper.style.paddingRight = 24;
            
            var content = new VisualElement();
            content.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            content.style.maxWidth = 860;
            content.style.paddingLeft = 40;
            content.style.paddingRight = 48;
            
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
            
            // 模块头部
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
                    item.style.color = new StyleColor(Theme.TextSecondary);
                }
                else
                {
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
            
            // 图标 + 标题行
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
            
            var icon = new Image { image = KitIcons.GetTexture(module.Icon) };
            icon.style.width = 26;
            icon.style.height = 26;
            iconBg.Add(icon);
            iconTitle.Add(iconBg);
            
            // 标题
            var title = new Label(module.Name);
            title.style.fontSize = 30;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Theme.TextPrimary);
            iconTitle.Add(title);
            
            header.Add(iconTitle);
            
            // 徽章行
            var badgeRow = new VisualElement();
            badgeRow.style.flexDirection = FlexDirection.Row;
            badgeRow.style.alignItems = Align.Center;
            badgeRow.style.marginTop = 12;
            badgeRow.style.marginLeft = 68;
            
            if (!string.IsNullOrEmpty(module.Category))
            {
                var categoryBadge = CreateBadge(module.Category, GetCategoryColor(module.Category), true);
                badgeRow.Add(categoryBadge);
            }
            
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
                desc.style.marginLeft = 68;
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
                
                var dot = new Image { image = KitIcons.GetTexture(KitIcons.DOT) };
                dot.style.width = 8;
                dot.style.height = 8;
                dot.tintColor = Theme.AccentGreen;
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
            
            // 代码块头部
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
            copyBtn.style.flexDirection = FlexDirection.Row;
            copyBtn.style.alignItems = Align.Center;
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
            
            var copyIcon = new Image { image = KitIcons.GetTexture(KitIcons.COPY) };
            copyIcon.style.width = 12;
            copyIcon.style.height = 12;
            copyIcon.style.marginRight = 4;
            copyBtn.Add(copyIcon);
            copyBtn.Add(new Label("复制"));
            
            codeHeader.Add(copyBtn);
            codeContainer.Add(codeHeader);
            
            // 代码内容
            var codeBlock = new VisualElement();
            codeBlock.style.backgroundColor = new StyleColor(Theme.BgCode);
            codeBlock.style.paddingLeft = 16;
            codeBlock.style.paddingRight = 16;
            codeBlock.style.paddingTop = 14;
            codeBlock.style.paddingBottom = 14;
            codeBlock.style.position = Position.Relative;
            
            // 底层：可选中的 TextField
            var codeTextField = new TextField();
            codeTextField.multiline = true;
            codeTextField.isReadOnly = true;
            codeTextField.value = example.Code;
            codeTextField.style.position = Position.Absolute;
            codeTextField.style.left = 16;
            codeTextField.style.right = 16;
            codeTextField.style.top = 14;
            codeTextField.style.bottom = 14;
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
                textInput.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.01f));
            }
            
            codeTextField.style.fontSize = 13;
#if UNITY_6000_0_OR_NEWER
            codeTextField.style.whiteSpace = WhiteSpace.Pre;
#else
            codeTextField.style.whiteSpace = WhiteSpace.NoWrap;
#endif
            codeTextField.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.01f));
            
            codeBlock.Add(codeTextField);
            
            // 顶层：语法高亮的 Label
            var highlightedCode = CSharpSyntaxHighlighter.Highlight(example.Code);
            var codeLabel = new Label();
            codeLabel.enableRichText = true;
            codeLabel.text = highlightedCode;
            codeLabel.style.fontSize = 13;
#if UNITY_6000_0_OR_NEWER
            codeLabel.style.whiteSpace = WhiteSpace.Pre;
#else
            codeLabel.style.whiteSpace = WhiteSpace.NoWrap;
#endif
            codeLabel.pickingMode = PickingMode.Ignore;
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
                explanationBox.style.paddingTop = 12;
                explanationBox.style.paddingBottom = 12;
                explanationBox.style.backgroundColor = new StyleColor(new Color(0.22f, 0.18f, 0.08f));
                explanationBox.style.borderTopLeftRadius = 6;
                explanationBox.style.borderTopRightRadius = 6;
                explanationBox.style.borderBottomLeftRadius = 6;
                explanationBox.style.borderBottomRightRadius = 6;
                explanationBox.style.borderLeftWidth = 3;
                explanationBox.style.borderLeftColor = new StyleColor(new Color(0.95f, 0.75f, 0.2f));
                
                var infoIcon = new Image { image = KitIcons.GetTexture(KitIcons.TIP) };
                infoIcon.style.width = 17;
                infoIcon.style.height = 17;
                infoIcon.style.marginRight = 12;
                explanationBox.Add(infoIcon);
                
                var explanation = new Label(example.Explanation);
                explanation.style.fontSize = 14;
                explanation.style.color = new StyleColor(new Color(0.9f, 0.85f, 0.7f));
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
    }
}
#endif
