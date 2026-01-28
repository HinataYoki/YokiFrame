#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && UNITY_2022_1_OR_NEWER
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YooInitConfig 属性绘制器 - 卡片组件
    /// </summary>
    public partial class YooInitConfigDrawer
    {
        #region 卡片

        /// <summary>
        /// EditorPrefs 键前缀
        /// </summary>
        private const string PREFS_KEY_PREFIX = "YokiFrame.YooInitConfig.Foldout.";

        /// <summary>
        /// 获取折叠状态的 EditorPrefs 键
        /// </summary>
        private static string GetFoldoutPrefsKey(string cardId) => PREFS_KEY_PREFIX + cardId;

        /// <summary>
        /// 创建配置卡片（带持久化折叠状态）
        /// </summary>
        /// <param name="title">卡片标题</param>
        /// <param name="iconId">图标 ID</param>
        /// <param name="cardId">卡片唯一标识（用于持久化折叠状态）</param>
        /// <param name="collapsible">是否可折叠</param>
        /// <param name="defaultExpanded">默认展开状态（仅首次使用时生效）</param>
        private static (VisualElement card, VisualElement body) CreateConfigCard(string title, string iconId, string cardId, bool collapsible = true, bool defaultExpanded = true)
        {
            // 从 EditorPrefs 读取折叠状态，如果没有则使用默认值
            bool isExpanded = EditorPrefs.GetBool(GetFoldoutPrefsKey(cardId), defaultExpanded);

            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(Colors.LayerCard);
            card.style.borderTopLeftRadius = Radius.LG;
            card.style.borderTopRightRadius = Radius.LG;
            card.style.borderBottomLeftRadius = Radius.LG;
            card.style.borderBottomRightRadius = Radius.LG;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftColor = new StyleColor(Colors.BorderDefault);
            card.style.borderRightColor = new StyleColor(Colors.BorderDefault);
            card.style.borderTopColor = new StyleColor(Colors.BorderDefault);
            card.style.borderBottomColor = new StyleColor(Colors.BorderDefault);

            var header = CreateCardHeader(title, iconId, collapsible, isExpanded);
            card.Add(header);

            var body = new VisualElement();
            body.style.paddingLeft = Spacing.MD;
            body.style.paddingRight = Spacing.MD;
            body.style.paddingTop = Spacing.SM;
            body.style.paddingBottom = Spacing.SM;
            body.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            card.Add(body);

            // 折叠交互
            if (collapsible)
            {
                var arrowLabel = header.Q<Label>("arrow-label");
                SetupCollapsibleBehavior(header, body, arrowLabel, cardId);
            }

            return (card, body);
        }

        /// <summary>
        /// 创建配置卡片（兼容旧版本，不带 cardId）
        /// </summary>
        private static (VisualElement card, VisualElement body) CreateConfigCard(string title, string iconId, bool collapsible = true, bool defaultExpanded = true)
        {
            // 使用标题作为 cardId
            return CreateConfigCard(title, iconId, title, collapsible, defaultExpanded);
        }

        /// <summary>
        /// 创建卡片头部
        /// </summary>
        private static VisualElement CreateCardHeader(string title, string iconId, bool collapsible, bool defaultExpanded)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = Spacing.MD;
            header.style.paddingRight = Spacing.MD;
            header.style.paddingTop = Spacing.SM;
            header.style.paddingBottom = Spacing.SM;
            header.style.backgroundColor = new StyleColor(Colors.LayerElevated);
            header.style.borderTopLeftRadius = Radius.LG;
            header.style.borderTopRightRadius = Radius.LG;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(Colors.BorderLight);

            // 折叠箭头
            if (collapsible)
            {
                var arrowLabel = new Label(defaultExpanded ? "▼" : "▶");
                arrowLabel.name = "arrow-label";
                arrowLabel.style.fontSize = 10;
                arrowLabel.style.color = new StyleColor(Colors.TextTertiary);
                arrowLabel.style.marginRight = Spacing.XS;
                arrowLabel.style.width = 12;
                arrowLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                header.Add(arrowLabel);
            }

            if (!string.IsNullOrEmpty(iconId))
            {
                var icon = new Image { image = KitIcons.GetTexture(iconId) };
                icon.style.width = 14;
                icon.style.height = 14;
                icon.style.marginRight = Spacing.XS;
                icon.tintColor = Colors.TextSecondary;
                header.Add(icon);
            }

            var titleLabel = new Label(title);
            titleLabel.style.color = new StyleColor(Colors.TextPrimary);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 12;
            titleLabel.style.flexGrow = 1;
            header.Add(titleLabel);

            // 初始状态下的圆角
            if (collapsible && !defaultExpanded)
            {
                header.style.borderBottomLeftRadius = Radius.LG;
                header.style.borderBottomRightRadius = Radius.LG;
                header.style.borderBottomWidth = 0;
            }

            return header;
        }

        /// <summary>
        /// 设置折叠行为（带持久化）
        /// </summary>
        private static void SetupCollapsibleBehavior(VisualElement header, VisualElement body, Label arrowLabel, string cardId)
        {
            header.RegisterCallback<MouseEnterEvent>(_ => header.style.backgroundColor = new StyleColor(new Color(Colors.LayerElevated.r * 1.1f, Colors.LayerElevated.g * 1.1f, Colors.LayerElevated.b * 1.1f)));
            header.RegisterCallback<MouseLeaveEvent>(_ => header.style.backgroundColor = new StyleColor(Colors.LayerElevated));
            header.RegisterCallback<ClickEvent>(_ =>
            {
                bool isExpanded = body.style.display == DisplayStyle.Flex;
                bool newState = !isExpanded;
                
                body.style.display = newState ? DisplayStyle.Flex : DisplayStyle.None;
                if (arrowLabel != null)
                    arrowLabel.text = newState ? "▼" : "▶";
                
                // 折叠时调整底部圆角
                header.style.borderBottomLeftRadius = newState ? 0 : Radius.LG;
                header.style.borderBottomRightRadius = newState ? 0 : Radius.LG;
                header.style.borderBottomWidth = newState ? 1 : 0;

                // 保存折叠状态到 EditorPrefs
                EditorPrefs.SetBool(GetFoldoutPrefsKey(cardId), newState);
            });
        }

        /// <summary>
        /// 创建加密配置卡片
        /// </summary>
        private static VisualElement CreateEncryptionCard(string title, string description, Color accentColor, VisualElement content)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(new Color(accentColor.r * 0.15f, accentColor.g * 0.15f, accentColor.b * 0.15f, 0.5f));
            card.style.borderTopLeftRadius = Radius.MD;
            card.style.borderTopRightRadius = Radius.MD;
            card.style.borderBottomLeftRadius = Radius.MD;
            card.style.borderBottomRightRadius = Radius.MD;
            card.style.borderLeftWidth = 2;
            card.style.borderLeftColor = new StyleColor(accentColor);
            card.style.paddingLeft = Spacing.MD;
            card.style.paddingRight = Spacing.MD;
            card.style.paddingTop = Spacing.SM;
            card.style.paddingBottom = Spacing.SM;

            var titleLabel = new Label(title);
            titleLabel.style.color = new StyleColor(accentColor);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 11;
            titleLabel.style.marginBottom = Spacing.XS;
            card.Add(titleLabel);

            var descLabel = new Label(description);
            descLabel.style.color = new StyleColor(Colors.TextSecondary);
            descLabel.style.fontSize = 10;
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.marginBottom = content != null ? Spacing.SM : 0;
            card.Add(descLabel);

            if (content != null)
                card.Add(content);

            return card;
        }

        #endregion
    }
}
#endif
