#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 标准 Kit 页面骨架组件。
    /// 为各个编辑器工具页提供统一的头部、工具栏、状态带与工作区壳层。
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        /// <summary>
        /// 标准 Kit 页面结构结果。
        /// </summary>
        public sealed class KitPageScaffold
        {
            /// <summary>
            /// 页面根节点。
            /// </summary>
            public VisualElement Root { get; set; }

            /// <summary>
            /// 顶部头图区。
            /// </summary>
            public VisualElement Hero { get; set; }

            /// <summary>
            /// 工具栏区域。
            /// </summary>
            public VisualElement Toolbar { get; set; }

            /// <summary>
            /// 状态说明区域。
            /// </summary>
            public VisualElement StatusBar { get; set; }

            /// <summary>
            /// 页面主工作区。
            /// </summary>
            public VisualElement Content { get; set; }
        }

        /// <summary>
        /// 创建标准 Kit 页面骨架。
        /// </summary>
        /// <param name="title">页面标题。</param>
        /// <param name="summary">页面摘要。</param>
        /// <param name="iconId">Kit 图标 ID。</param>
        /// <param name="eyebrow">头部辅助标签。</param>
        /// <param name="headerActions">头部右侧动作区。</param>
        /// <returns>页面骨架结果。</returns>
        public static KitPageScaffold CreateKitPageScaffold(
            string title,
            string summary,
            string iconId = null,
            string eyebrow = null,
            VisualElement headerActions = null)
        {
            var root = new VisualElement();
            root.AddToClassList("yoki-kit-page");

            var hero = CreateKitHeroHeader(title, summary, iconId, eyebrow, headerActions);
            root.Add(hero);

            var toolbar = new VisualElement();
            toolbar.AddToClassList("yoki-kit-page__toolbar");
            root.Add(toolbar);

            var statusBar = new VisualElement();
            statusBar.AddToClassList("yoki-kit-page__status");
            root.Add(statusBar);

            var content = new VisualElement();
            content.AddToClassList("yoki-kit-page__content");
            root.Add(content);

            return new KitPageScaffold
            {
                Root = root,
                Hero = hero,
                Toolbar = toolbar,
                StatusBar = statusBar,
                Content = content
            };
        }

        /// <summary>
        /// 创建标准头图区。
        /// </summary>
        public static VisualElement CreateKitHeroHeader(
            string title,
            string summary,
            string iconId = null,
            string eyebrow = null,
            VisualElement headerActions = null)
        {
            var hero = new VisualElement();
            hero.AddToClassList("yoki-kit-hero");

            var leading = new VisualElement();
            leading.AddToClassList("yoki-kit-hero__leading");
            hero.Add(leading);

            if (!string.IsNullOrEmpty(iconId))
            {
                var iconWrap = new VisualElement();
                iconWrap.AddToClassList("yoki-kit-hero__icon-wrap");

                var icon = new Image { image = KitIcons.GetTexture(iconId) };
                icon.AddToClassList("yoki-kit-hero__icon");
                iconWrap.Add(icon);
                leading.Add(iconWrap);
            }

            var textGroup = new VisualElement();
            textGroup.AddToClassList("yoki-kit-hero__text");
            leading.Add(textGroup);

            if (!string.IsNullOrEmpty(eyebrow))
            {
                var eyebrowLabel = new Label(eyebrow);
                eyebrowLabel.AddToClassList("yoki-kit-hero__eyebrow");
                textGroup.Add(eyebrowLabel);
            }

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("yoki-kit-hero__title");
            textGroup.Add(titleLabel);

            if (!string.IsNullOrEmpty(summary))
            {
                var summaryLabel = new Label(summary);
                summaryLabel.AddToClassList("yoki-kit-hero__summary");
                textGroup.Add(summaryLabel);
            }

            var actions = new VisualElement();
            actions.AddToClassList("yoki-kit-hero__actions");
            if (headerActions != null)
            {
                actions.Add(headerActions);
            }

            hero.Add(actions);
            return hero;
        }

        /// <summary>
        /// 创建标准状态横幅。
        /// </summary>
        public static VisualElement CreateKitStatusBanner(
            string title,
            string message,
            HelpBoxType type = HelpBoxType.Info)
        {
            var banner = new VisualElement();
            banner.AddToClassList("yoki-kit-banner");

            switch (type)
            {
                case HelpBoxType.Warning:
                    banner.AddToClassList("yoki-kit-banner--warning");
                    break;
                case HelpBoxType.Error:
                    banner.AddToClassList("yoki-kit-banner--error");
                    break;
                case HelpBoxType.Success:
                    banner.AddToClassList("yoki-kit-banner--success");
                    break;
                default:
                    banner.AddToClassList("yoki-kit-banner--info");
                    break;
            }

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("yoki-kit-banner__title");
            banner.Add(titleLabel);

            var messageLabel = new Label(message);
            messageLabel.AddToClassList("yoki-kit-banner__message");
            banner.Add(messageLabel);

            return banner;
        }

        /// <summary>
        /// 创建标准指标带。
        /// </summary>
        public static VisualElement CreateKitMetricStrip()
        {
            var strip = new VisualElement();
            strip.AddToClassList("yoki-kit-metric-strip");
            return strip;
        }

        /// <summary>
        /// 创建标准指标卡片。
        /// </summary>
        public static (VisualElement card, Label valueLabel) CreateKitMetricCard(
            string title,
            string value,
            string hint = null,
            Color? accentColor = null)
        {
            var card = new VisualElement();
            card.AddToClassList("yoki-kit-metric-card");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("yoki-kit-metric-card__title");
            card.Add(titleLabel);

            var valueLabel = new Label(value);
            valueLabel.AddToClassList("yoki-kit-metric-card__value");
            if (accentColor.HasValue)
            {
                valueLabel.style.color = new StyleColor(accentColor.Value);
            }

            card.Add(valueLabel);

            if (!string.IsNullOrEmpty(hint))
            {
                var hintLabel = new Label(hint);
                hintLabel.AddToClassList("yoki-kit-metric-card__hint");
                card.Add(hintLabel);
            }

            return (card, valueLabel);
        }

        /// <summary>
        /// 创建标准分区面板。
        /// </summary>
        public static (VisualElement panel, VisualElement body) CreateKitSectionPanel(
            string title,
            string summary = null,
            string iconId = null,
            VisualElement trailing = null)
        {
            var panel = new VisualElement();
            panel.AddToClassList("yoki-kit-panel");

            var header = new VisualElement();
            header.AddToClassList("yoki-kit-panel__header");
            panel.Add(header);

            var headerText = new VisualElement();
            headerText.AddToClassList("yoki-kit-panel__header-text");
            header.Add(headerText);

            var titleRow = new VisualElement();
            titleRow.AddToClassList("yoki-kit-panel__title-row");
            headerText.Add(titleRow);

            if (!string.IsNullOrEmpty(iconId))
            {
                var icon = new Image { image = KitIcons.GetTexture(iconId) };
                icon.AddToClassList("yoki-kit-panel__icon");
                titleRow.Add(icon);
            }

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("yoki-kit-panel__title");
            titleRow.Add(titleLabel);

            if (!string.IsNullOrEmpty(summary))
            {
                var summaryLabel = new Label(summary);
                summaryLabel.AddToClassList("yoki-kit-panel__summary");
                headerText.Add(summaryLabel);
            }

            if (trailing != null)
            {
                var trailingWrap = new VisualElement();
                trailingWrap.AddToClassList("yoki-kit-panel__trailing");
                trailingWrap.Add(trailing);
                header.Add(trailingWrap);
            }

            var body = new VisualElement();
            body.AddToClassList("yoki-kit-panel__body");
            panel.Add(body);

            return (panel, body);
        }
    }
}
#endif
