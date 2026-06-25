#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.Unity
{
    public sealed partial class YooAssetWorkbenchWindow
    {
        private VisualElement BuildHero()
        {
            var hero = new VisualElement();
            hero.AddToClassList("yoki-kit-hero");
            hero.style.flexDirection = FlexDirection.Row;
            hero.style.alignItems = Align.Center;
            hero.style.justifyContent = Justify.SpaceBetween;
            hero.style.minHeight = 78f;
            hero.style.flexShrink = 0f;
            hero.style.paddingLeft = 16f;
            hero.style.paddingRight = 16f;
            hero.style.paddingTop = 14f;
            hero.style.paddingBottom = 14f;
            hero.style.marginBottom = 10f;
            hero.style.backgroundColor = new StyleColor(new Color(0.13f, 0.59f, 0.95f, 0.06f));
            SetBorder(hero, new Color(0.13f, 0.59f, 0.95f, 0.14f));
            SetRadius(hero, 8f);

            var leading = new VisualElement();
            leading.AddToClassList("yoki-kit-hero__leading");
            leading.style.flexDirection = FlexDirection.Row;
            leading.style.alignItems = Align.Center;
            leading.style.flexGrow = 1f;
            leading.style.minWidth = 0f;
            hero.Add(leading);

            var iconWrap = new VisualElement();
            iconWrap.AddToClassList("yoki-kit-hero__icon-wrap");
            iconWrap.style.width = 44f;
            iconWrap.style.height = 44f;
            iconWrap.style.flexShrink = 0f;
            iconWrap.style.marginRight = 14f;
            iconWrap.style.justifyContent = Justify.Center;
            iconWrap.style.alignItems = Align.Center;
            iconWrap.style.backgroundColor = new StyleColor(new Color(0.13f, 0.59f, 0.95f, 0.12f));
            SetBorder(iconWrap, new Color(0.13f, 0.59f, 0.95f, 0.16f));
            SetRadius(iconWrap, 8f);
            leading.Add(iconWrap);

            var icon = new Image { image = KitIcons.GetTexture(KitIcons.RESKIT) };
            icon.AddToClassList("yoki-kit-hero__icon");
            icon.style.width = 22f;
            icon.style.height = 22f;
            icon.tintColor = YokiFrameUIComponents.Colors.BrandPrimary;
            iconWrap.Add(icon);

            var text = new VisualElement();
            text.AddToClassList("yoki-kit-hero__text");
            text.style.flexGrow = 1f;
            text.style.minWidth = 0f;
            text.style.justifyContent = Justify.Center;
            leading.Add(text);

            var eyebrow = new Label("RESKIT / YOOASSET");
            eyebrow.AddToClassList("yoki-kit-hero__eyebrow");
            eyebrow.style.marginBottom = 2f;
            eyebrow.style.color = new StyleColor(YokiFrameUIComponents.Colors.BrandPrimary);
            eyebrow.style.fontSize = 10f;
            eyebrow.style.unityFontStyleAndWeight = FontStyle.Bold;
            text.Add(eyebrow);

            var title = new Label("YooAsset 资源采集器");
            title.AddToClassList("yoki-kit-hero__title");
            title.style.marginBottom = 2f;
            title.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
            title.style.fontSize = 17f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            text.Add(title);

            var summary = new Label("直接使用 Unity Editor 内的 YooAsset 配置数据，保持与 1.0 UIToolkit 工作台一致的卡片、工具栏与分组导航体验。");
            summary.AddToClassList("yoki-kit-hero__summary");
            summary.style.maxWidth = 640f;
            summary.style.whiteSpace = WhiteSpace.Normal;
            summary.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextSecondary);
            summary.style.fontSize = 12f;
            text.Add(summary);

            var actions = new VisualElement();
            actions.AddToClassList("yoki-kit-hero__actions");
            actions.style.minWidth = 220f;
            actions.style.marginLeft = 12f;
            actions.style.justifyContent = Justify.Center;
            actions.style.alignItems = Align.FlexEnd;
            actions.style.flexShrink = 0f;
            hero.Add(actions);

            var actionRow = new VisualElement();
            actionRow.AddToClassList("yoki-kit-hero-tools");
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.alignItems = Align.Center;
            actions.Add(actionRow);

            actionRow.Add(YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.POPOUT, "原生收集器", OpenNativeCollector));
            actionRow.Add(YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.POPOUT, "资源构建器", OpenNativeBuilder));

            return hero;
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = YokiFrameUIComponents.CreateToolbar();
            toolbar.AddToClassList("yoki-kit-inline-toolbar");

            var packageLabel = new Label("资源包");
            packageLabel.AddToClassList("toolbar-label");
            packageLabel.style.marginRight = 6f;
            toolbar.Add(packageLabel);

            mPackageDropdown = new DropdownField();
            mPackageDropdown.AddToClassList("yoo-package-dropdown");
            mPackageDropdown.style.minWidth = 180f;
            mPackageDropdown.style.maxWidth = 240f;
            mPackageDropdown.RegisterValueChangedCallback(OnPackageChanged);
            toolbar.Add(mPackageDropdown);

            toolbar.Add(CreateSquareToolbarButton("+", "新建资源包", AddPackage));
            toolbar.Add(CreateSquareToolbarButton("-", "删除当前资源包", RemoveCurrentPackage));

            toolbar.Add(YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.SETTINGS, "包设置", TogglePackageSettings));
            toolbar.Add(YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.SETTINGS, "全局设置", ToggleGlobalSettings));
            toolbar.Add(YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.REFRESH, "刷新", RefreshAll));
            toolbar.Add(YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.RESET, "修复", FixSettings));

            toolbar.Add(YokiFrameUIComponents.CreateFlexSpacer());

            mUnsavedLabel = new Label("有未保存的更改");
            mUnsavedLabel.AddToClassList("yoki-kit-banner__message");
            mUnsavedLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.BrandWarning);
            mUnsavedLabel.style.display = DisplayStyle.None;
            toolbar.Add(mUnsavedLabel);

            toolbar.Add(YokiFrameUIComponents.CreateToolbarPrimaryButton("保存", SaveSettings));

            return toolbar;
        }

        private VisualElement BuildGlobalSettingsPanel()
        {
            var panel = CreateSettingsPanel("全局设置", KitIcons.SETTINGS);
            return panel;
        }

        private VisualElement BuildPackageSettingsPanel()
        {
            var panel = CreateSettingsPanel("包设置", KitIcons.PACKAGE);
            return panel;
        }

        private VisualElement BuildGroupNav()
        {
            var nav = new VisualElement();
            nav.name = "yoo-group-nav";
            nav.AddToClassList("yoo-group-nav");
            nav.AddToClassList("yoki-kit-panel");
            nav.AddToClassList("yoki-kit-panel--slate");
            nav.style.width = 260f;
            nav.style.minWidth = 220f;
            nav.style.maxWidth = 340f;
            nav.style.flexShrink = 0f;
            nav.style.flexDirection = FlexDirection.Column;
            nav.style.minHeight = 0f;
            nav.style.marginRight = 10f;
            nav.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.LayerCard);
            nav.style.overflow = Overflow.Hidden;
            SetBorder(nav, YokiFrameUIComponents.Colors.BorderDefault);
            SetRadius(nav, 8f);

            mGroupCountBadge = YokiFrameUIComponents.CreateCountLabel(0, YokiFrameUIComponents.Colors.BadgeDefault);
            nav.Add(CreatePanelHeader("分组", KitIcons.FOLDER, "双击分组名称可重命名，右键可删除。", mGroupCountBadge));

            var body = new VisualElement();
            body.AddToClassList("yoki-kit-panel__body");
            body.style.flexDirection = FlexDirection.Column;
            body.style.flexGrow = 1f;
            body.style.minHeight = 0f;
            nav.Add(body);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1f;
            scrollView.style.minHeight = 0f;
            body.Add(scrollView);

            mGroupList = new VisualElement();
            mGroupList.style.paddingLeft = 8f;
            mGroupList.style.paddingRight = 8f;
            mGroupList.style.paddingTop = 8f;
            mGroupList.style.paddingBottom = 8f;
            scrollView.Add(mGroupList);

            var footer = new VisualElement();
            footer.style.paddingLeft = 10f;
            footer.style.paddingRight = 10f;
            footer.style.paddingTop = 10f;
            footer.style.paddingBottom = 12f;
            footer.style.borderTopWidth = 1f;
            footer.style.borderTopColor = new StyleColor(YokiFrameUIComponents.Colors.BorderLight);
            footer.style.flexShrink = 0f;
            body.Add(footer);

            footer.Add(YokiFrameUIComponents.CreatePrimaryButton("+ 新建分组", AddGroup));

            return nav;
        }

        private VisualElement BuildCollectorCanvas()
        {
            var canvas = new VisualElement();
            canvas.name = "yoo-collector-canvas";
            canvas.AddToClassList("yoo-collector-canvas");
            canvas.AddToClassList("yoki-kit-panel");
            canvas.AddToClassList("yoki-kit-panel--blue");
            canvas.style.flexDirection = FlexDirection.Column;
            canvas.style.flexGrow = 1f;
            canvas.style.minWidth = 0f;
            canvas.style.minHeight = 0f;
            canvas.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.LayerCard);
            canvas.style.overflow = Overflow.Hidden;
            SetBorder(canvas, YokiFrameUIComponents.Colors.BorderDefault);
            SetRadius(canvas, 8f);

            mCollectorCountBadge = YokiFrameUIComponents.CreateCountLabel(0, YokiFrameUIComponents.Colors.BadgeDefault);
            canvas.Add(CreatePanelHeader("收集器", KitIcons.DOCUMENT, "选择左侧分组查看并编辑采集路径、规则和标签。", mCollectorCountBadge));

            var body = new VisualElement();
            body.AddToClassList("yoki-kit-panel__body");
            body.style.flexDirection = FlexDirection.Column;
            body.style.flexGrow = 1f;
            body.style.minHeight = 0f;
            canvas.Add(body);

            mGroupSummaryLabel = new Label("选择一个分组查看收集器");
            mGroupSummaryLabel.style.paddingLeft = 16f;
            mGroupSummaryLabel.style.paddingRight = 16f;
            mGroupSummaryLabel.style.paddingTop = 10f;
            mGroupSummaryLabel.style.paddingBottom = 8f;
            mGroupSummaryLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextSecondary);
            mGroupSummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            body.Add(mGroupSummaryLabel);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1f;
            scrollView.style.minHeight = 0f;
            body.Add(scrollView);

            mCollectorList = new VisualElement();
            mCollectorList.style.paddingLeft = 14f;
            mCollectorList.style.paddingRight = 14f;
            mCollectorList.style.paddingTop = 8f;
            mCollectorList.style.paddingBottom = 10f;
            scrollView.Add(mCollectorList);

            var footer = new VisualElement();
            footer.style.paddingLeft = 14f;
            footer.style.paddingRight = 14f;
            footer.style.paddingTop = 10f;
            footer.style.paddingBottom = 14f;
            footer.style.borderTopWidth = 1f;
            footer.style.borderTopColor = new StyleColor(YokiFrameUIComponents.Colors.BorderLight);
            footer.style.flexShrink = 0f;
            body.Add(footer);

            footer.Add(YokiFrameUIComponents.CreateSecondaryButton("+ 添加收集器", AddCollector));

            return canvas;
        }

        private VisualElement CreateSettingsPanel(string title, string iconId)
        {
            var panel = new VisualElement();
            panel.AddToClassList("yoki-kit-panel");
            panel.AddToClassList("yoki-kit-panel--amber");
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.flexGrow = 0f;
            panel.style.marginTop = 10f;
            panel.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.LayerCard);
            panel.style.overflow = Overflow.Hidden;
            SetBorder(panel, YokiFrameUIComponents.Colors.BorderDefault);
            SetRadius(panel, 8f);
            panel.Add(CreatePanelHeader(title, iconId, "这些选项直接写回 YooAsset 的 Collector Setting。", null));
            var body = new VisualElement();
            body.AddToClassList("yoki-kit-panel__body");
            body.style.paddingLeft = 14f;
            body.style.paddingRight = 14f;
            body.style.paddingTop = 12f;
            body.style.paddingBottom = 12f;
            panel.Add(body);
            return panel;
        }

        private static VisualElement CreatePanelHeader(string title, string iconId, string summary, VisualElement trailing)
        {
            var header = new VisualElement();
            header.AddToClassList("yoki-kit-panel__header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.minHeight = 64f;
            header.style.paddingLeft = 14f;
            header.style.paddingRight = 14f;
            header.style.paddingTop = 12f;
            header.style.paddingBottom = 12f;
            header.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.LayerElevated);
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = new StyleColor(YokiFrameUIComponents.Colors.BorderLight);

            var text = new VisualElement();
            text.AddToClassList("yoki-kit-panel__header-text");
            text.style.flexGrow = 1f;
            text.style.minWidth = 0f;
            header.Add(text);

            var titleRow = new VisualElement();
            titleRow.AddToClassList("yoki-kit-panel__title-row");
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            text.Add(titleRow);

            var icon = new Image { image = KitIcons.GetTexture(iconId) };
            icon.AddToClassList("yoki-kit-panel__icon");
            icon.style.width = 16f;
            icon.style.height = 16f;
            icon.style.marginRight = 8f;
            icon.tintColor = YokiFrameUIComponents.Colors.TextSecondary;
            titleRow.Add(icon);

            var label = new Label(title);
            label.AddToClassList("yoki-kit-panel__title");
            label.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
            label.style.fontSize = 13f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleRow.Add(label);

            if (!string.IsNullOrEmpty(summary))
            {
                var summaryLabel = new Label(summary);
                summaryLabel.AddToClassList("yoki-kit-panel__summary");
                summaryLabel.style.marginTop = 4f;
                summaryLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextSecondary);
                summaryLabel.style.fontSize = 11f;
                summaryLabel.style.whiteSpace = WhiteSpace.Normal;
                text.Add(summaryLabel);
            }

            if (trailing != null)
            {
                var trailingContainer = new VisualElement();
                trailingContainer.AddToClassList("yoki-kit-panel__trailing");
                trailingContainer.style.marginLeft = 12f;
                trailingContainer.style.flexShrink = 0f;
                trailingContainer.style.justifyContent = Justify.Center;
                trailingContainer.Add(trailing);
                header.Add(trailingContainer);
            }

            return header;
        }

        private static Button CreateSquareToolbarButton(string text, string tooltip, Action action)
        {
            var button = YokiFrameUIComponents.CreateToolbarButton(text, action);
            button.tooltip = tooltip;
            button.style.width = 32f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            return button;
        }

        private static void SetBorder(VisualElement element, Color color)
        {
            element.style.borderLeftWidth = 1f;
            element.style.borderRightWidth = 1f;
            element.style.borderTopWidth = 1f;
            element.style.borderBottomWidth = 1f;
            element.style.borderLeftColor = new StyleColor(color);
            element.style.borderRightColor = new StyleColor(color);
            element.style.borderTopColor = new StyleColor(color);
            element.style.borderBottomColor = new StyleColor(color);
        }

        private static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }
    }
}
#endif
#endif