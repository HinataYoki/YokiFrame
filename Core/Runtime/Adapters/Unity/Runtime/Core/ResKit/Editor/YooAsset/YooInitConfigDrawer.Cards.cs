#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.Unity
{
    public sealed partial class YooInitConfigDrawer
    {
        private static VisualElement CreateCard(VisualElement root, string title, string name, bool expanded)
        {
            var isExpanded = EditorPrefs.GetBool(FOLDOUT_PREFS_PREFIX + name, expanded);
            var card = new VisualElement();
            card.name = name;
            card.AddToClassList("yoo-config-card");
            card.AddToClassList("card");
            card.AddToClassList("yoki-card");
            card.style.backgroundColor = sCardBackground;
            card.style.borderTopLeftRadius = YokiFrameUIComponents.Radius.LG;
            card.style.borderTopRightRadius = YokiFrameUIComponents.Radius.LG;
            card.style.borderBottomLeftRadius = YokiFrameUIComponents.Radius.LG;
            card.style.borderBottomRightRadius = YokiFrameUIComponents.Radius.LG;
            card.style.borderLeftWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderTopWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftColor = sBorderColor;
            card.style.borderRightColor = sBorderColor;
            card.style.borderTopColor = sBorderColor;
            card.style.borderBottomColor = sBorderColor;
            card.style.overflow = Overflow.Hidden;
            card.style.marginLeft = 0f;
            card.style.marginRight = 0f;
            card.style.marginTop = 0f;
            card.style.marginBottom = YokiFrameUIComponents.Spacing.MD;
            root.Add(card);

            var header = new VisualElement();
            header.AddToClassList("card-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.backgroundColor = sCardHeaderBackground;
            header.style.paddingLeft = YokiFrameUIComponents.Spacing.MD;
            header.style.paddingRight = YokiFrameUIComponents.Spacing.MD;
            header.style.paddingTop = YokiFrameUIComponents.Spacing.SM;
            header.style.paddingBottom = YokiFrameUIComponents.Spacing.SM;
            card.Add(header);

            var arrow = new Label(isExpanded ? "▼" : "▶");
            arrow.name = "arrow-label";
            arrow.style.width = 14f;
            arrow.style.color = sTextSecondary;
            arrow.style.fontSize = 10f;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.Add(arrow);

            var icon = new Image { image = KitIcons.GetTexture(GetCardIcon(name)) };
            icon.style.width = 16f;
            icon.style.height = 16f;
            icon.style.marginLeft = 2f;
            icon.style.marginRight = 8f;
            icon.tintColor = sTextMuted;
            header.Add(icon);

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("card-title");
            titleLabel.style.color = sTextPrimary;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 12f;
            titleLabel.style.flexGrow = 1f;
            header.Add(titleLabel);

            var body = new VisualElement();
            body.AddToClassList("card-body");
            body.style.backgroundColor = sCardBackground;
            body.style.paddingLeft = YokiFrameUIComponents.Spacing.MD;
            body.style.paddingRight = YokiFrameUIComponents.Spacing.MD;
            body.style.paddingTop = YokiFrameUIComponents.Spacing.SM;
            body.style.paddingBottom = YokiFrameUIComponents.Spacing.MD;
            body.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            card.Add(body);

            header.RegisterCallback<MouseEnterEvent>(_ =>
            {
                header.style.backgroundColor = YokiFrameUIComponents.Colors.LayerHover;
            });
            header.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                header.style.backgroundColor = sCardHeaderBackground;
            });
            header.RegisterCallback<ClickEvent>(_ =>
            {
                var next = body.style.display == DisplayStyle.None;
                body.style.display = next ? DisplayStyle.Flex : DisplayStyle.None;
                arrow.text = next ? "▼" : "▶";
                EditorPrefs.SetBool(FOLDOUT_PREFS_PREFIX + name, next);
            });

            return body;
        }

        private static string GetCardIcon(string name)
        {
            if (name == "yoo-init-basic-card")
                return KitIcons.SETTINGS;

            if (name == "yoo-init-encryption-card")
                return KitIcons.KEYBOARD;

            if (name == "yoo-init-build-card")
                return KitIcons.PACKAGE;

            return KitIcons.RESKIT;
        }
    }
}
#endif
#endif