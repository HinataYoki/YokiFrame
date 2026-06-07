#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Content renderer for the documentation page.
    /// </summary>
    public partial class DocumentationToolPage
    {
        #region Content Entry

        /// <summary>
        /// Renders the selected documentation module into the content area.
        /// </summary>
        private void RenderContent(DocModule module)
        {
            mContentScrollView.Clear();
            mCurrentHeadings.Clear();

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
            content.style.opacity = 0;
            content.style.translate = new Translate(0, 10);
            content.style.transitionProperty = new List<StylePropertyName> { new("opacity"), new("translate") };
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

            content.Add(CreateBreadcrumb(module));

            var header = CreateModuleHeader(module);
            content.Add(header);
            mCurrentHeadings.Add((module.Name, header, 1));

            if (module.Sections != null)
            {
                foreach (var section in module.Sections)
                {
                    if (section == null)
                    {
                        continue;
                    }

                    var sectionElement = CreateSectionElement(section);
                    content.Add(sectionElement);
                    mCurrentHeadings.Add((section.Title, sectionElement, 2));
                }
            }

            RefreshOnThisPage();

            content.schedule.Execute(() =>
            {
                content.style.opacity = 1;
                content.style.translate = new Translate(0, 0);
            }).ExecuteLater(16);
        }

        #endregion

        #region Breadcrumb

        /// <summary>
        /// Creates the breadcrumb shown above the module content.
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
                    var separator = new Label(">");
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
                    item.RegisterCallback<MouseEnterEvent>(_ => item.style.color = new StyleColor(Theme.AccentBlue));
                    item.RegisterCallback<MouseLeaveEvent>(_ => item.style.color = new StyleColor(Theme.TextMuted));
                }

                breadcrumb.Add(item);
            }

            return breadcrumb;
        }

        #endregion

        #region Module Header

        /// <summary>
        /// Creates the top header block for a documentation module.
        /// </summary>
        private VisualElement CreateModuleHeader(DocModule module)
        {
            var header = new VisualElement();
            header.style.marginBottom = 32;
            header.style.paddingBottom = 24;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(Theme.Border);

            var categoryColor = GetCategoryColor(module.Category);
            header.Add(CreateHeaderRow(CreateModuleTitleLeading(module, categoryColor)));

            var badgeRow = new VisualElement();
            badgeRow.style.flexDirection = FlexDirection.Row;
            badgeRow.style.alignItems = Align.Center;
            badgeRow.style.marginTop = 12;
            badgeRow.style.marginLeft = 68;

            if (!string.IsNullOrEmpty(module.Category))
            {
                badgeRow.Add(CreateBadge(module.Category, categoryColor, true));
            }

            if (module.Keywords != null && module.Keywords.Count > 0)
            {
                foreach (var keyword in module.Keywords)
                {
                    if (string.IsNullOrEmpty(keyword))
                    {
                        continue;
                    }

                    var keywordBadge = CreateBadge(keyword, Theme.TextMuted, false);
                    keywordBadge.style.marginLeft = 8;
                    badgeRow.Add(keywordBadge);
                }
            }

            header.Add(badgeRow);

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

            if (module.PluginLinks != null && module.PluginLinks.Count > 0)
            {
                header.Add(CreatePluginLinkRow(module.PluginLinks));
            }

            return header;
        }

        private VisualElement CreateModuleTitleLeading(DocModule module, Color categoryColor)
        {
            var leading = new VisualElement();
            leading.style.flexDirection = FlexDirection.Row;
            leading.style.alignItems = Align.Center;

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
                categoryColor.b * 0.3f));
            iconBg.style.alignItems = Align.Center;
            iconBg.style.justifyContent = Justify.Center;
            iconBg.style.marginRight = 16;

            var icon = new Image { image = KitIcons.GetTexture(module.Icon) };
            icon.style.width = 26;
            icon.style.height = 26;
            iconBg.Add(icon);
            leading.Add(iconBg);

            var title = new Label(module.Name);
            title.style.fontSize = 30;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Theme.TextPrimary);
            leading.Add(title);

            return leading;
        }

        /// <summary>
        /// Creates a category or keyword badge.
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

        /// <summary>
        /// Creates a row of plugin link buttons shown below the module description.
        /// Each button opens the plugin's official website or GitHub page.
        /// </summary>
        private VisualElement CreatePluginLinkRow(List<PluginLink> pluginLinks)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 20;
            row.style.marginLeft = 68;

            var label = new Label("可选插件：");
            label.style.fontSize = 13;
            label.style.color = new StyleColor(Theme.TextMuted);
            label.style.marginRight = 10;
            row.Add(label);

            foreach (var link in pluginLinks)
            {
                if (link == null || string.IsNullOrEmpty(link.Name) || string.IsNullOrEmpty(link.Url))
                {
                    continue;
                }

                row.Add(CreatePluginLinkButton(link));
            }

            return row;
        }

        /// <summary>
        /// Creates a single plugin link button that opens the URL in the default browser.
        /// </summary>
        private Button CreatePluginLinkButton(PluginLink link)
        {
            var button = new Button(() => UnityEngine.Application.OpenURL(link.Url));
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.fontSize = 12;
            button.style.paddingLeft = 12;
            button.style.paddingRight = 12;
            button.style.paddingTop = 6;
            button.style.paddingBottom = 6;
            button.style.marginRight = 10;
            button.style.borderTopLeftRadius = 6;
            button.style.borderTopRightRadius = 6;
            button.style.borderBottomLeftRadius = 6;
            button.style.borderBottomRightRadius = 6;
            button.style.backgroundColor = new StyleColor(new Color(0.14f, 0.17f, 0.22f));
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.5f, 0.7f));
            button.style.borderRightColor = new StyleColor(new Color(0.3f, 0.5f, 0.7f));
            button.style.borderTopColor = new StyleColor(new Color(0.3f, 0.5f, 0.7f));
            button.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.5f, 0.7f));
            button.style.color = new StyleColor(new Color(0.55f, 0.75f, 0.95f));

            var icon = new Image { image = KitIcons.GetTexture(KitIcons.POPOUT) };
            icon.style.width = 13;
            icon.style.height = 13;
            icon.style.marginRight = 6;
            button.Add(icon);

            var label = new Label(link.Name);
            label.style.fontSize = 12;
            button.Add(label);

            button.RegisterCallback<MouseEnterEvent>(_ =>
            {
                button.style.backgroundColor = new StyleColor(new Color(0.18f, 0.23f, 0.30f));
                button.style.borderLeftColor = new StyleColor(new Color(0.4f, 0.65f, 0.9f));
                button.style.borderRightColor = new StyleColor(new Color(0.4f, 0.65f, 0.9f));
                button.style.borderTopColor = new StyleColor(new Color(0.4f, 0.65f, 0.9f));
                button.style.borderBottomColor = new StyleColor(new Color(0.4f, 0.65f, 0.9f));
            });

            button.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                button.style.backgroundColor = new StyleColor(new Color(0.14f, 0.17f, 0.22f));
                button.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.5f, 0.7f));
                button.style.borderRightColor = new StyleColor(new Color(0.3f, 0.5f, 0.7f));
                button.style.borderTopColor = new StyleColor(new Color(0.3f, 0.5f, 0.7f));
                button.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.5f, 0.7f));
            });

            return button;
        }

        /// <summary>
        /// Creates a compact row of link buttons shown below a section description.
        /// </summary>
        private VisualElement CreateSectionLinkRow(List<PluginLink> links)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 20;
            row.style.marginLeft = 18;

            foreach (var link in links)
            {
                if (link == null || string.IsNullOrEmpty(link.Name) || string.IsNullOrEmpty(link.Url))
                {
                    continue;
                }

                var linkButton = CreatePluginLinkButton(link);
                linkButton.style.fontSize = 11;
                linkButton.style.paddingLeft = 10;
                linkButton.style.paddingRight = 10;
                linkButton.style.paddingTop = 4;
                linkButton.style.paddingBottom = 4;

                var icon = linkButton.Q<Image>();
                if (icon != null)
                {
                    icon.style.width = 11;
                    icon.style.height = 11;
                }

                row.Add(linkButton);
            }

            return row;
        }

        #endregion

        #region Sections

        /// <summary>
        /// Creates the visual block for one documentation section.
        /// </summary>
        private VisualElement CreateSectionElement(DocSection section)
        {
            var container = new VisualElement();
            container.style.marginBottom = 40;

            var titleRow = CreateAccentTitle(section.Title, Theme.AccentBlue);
            titleRow.style.marginBottom = 16;
            container.Add(titleRow);

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

            if (section.Links != null && section.Links.Count > 0)
            {
                var linkRow = CreateSectionLinkRow(section.Links);
                container.Add(linkRow);
            }

            if (section.CodeExamples != null)
            {
                foreach (var example in section.CodeExamples)
                {
                    if (example != null)
                    {
                        container.Add(CreateCodeExampleElement(example, mRootContainer));
                    }
                }
            }

            return container;
        }

        #endregion
    }
}
#endif
