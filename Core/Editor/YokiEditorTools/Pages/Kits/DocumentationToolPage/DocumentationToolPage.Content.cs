#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 文档页面 - 内容渲染
    /// </summary>
    public partial class DocumentationToolPage
    {
        #region 内容渲染入口

        /// <summary>
        /// 渲染模块内容
        /// </summary>
        private void RenderContent(DocModule module)
        {
            mContentScrollView.Clear();
            mCurrentHeadings.Clear();

            // 居中内容容器（不设置 flexGrow，让高度由内容决定）
            var centerWrapper = new VisualElement();
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

        #endregion

        #region 面包屑导航

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

        #endregion

        #region 模块头部

        /// <summary>
        /// 创建模块头部
        /// </summary>
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
            var categoryColor = GetCategoryColor(module.Category);
            var iconBg = new VisualElement();
            iconBg.style.width = 52;
            iconBg.style.height = 52;
            iconBg.style.borderTopLeftRadius = 12;
            iconBg.style.borderTopRightRadius = 12;
            iconBg.style.borderBottomLeftRadius = 12;
            iconBg.style.borderBottomRightRadius = 12;
            iconBg.style.backgroundColor = new StyleColor(new Color(
                categoryColor.r * 0.3f,
                categoryColor.g * 0.3f,
                categoryColor.b * 0.3f
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
                var categoryBadge = CreateBadge(module.Category, categoryColor, true);
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

        #endregion

        #region 章节元素

        /// <summary>
        /// 创建章节元素
        /// </summary>
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
                container.Add(CreateCodeExampleElement(example, mRootContainer));
            }

            return container;
        }

        #endregion
    }
}
#endif
