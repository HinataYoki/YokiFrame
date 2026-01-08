#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - 卡片与容器
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region 卡片

        /// <summary>
        /// 创建现代化卡片容器
        /// </summary>
        /// <param name="title">卡片标题（可选）</param>
        /// <param name="icon">标题图标（可选，如 "📁"）</param>
        public static (VisualElement card, VisualElement body) CreateCard(string title = null, string icon = null)
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            
            VisualElement body;
            
            if (!string.IsNullOrEmpty(title))
            {
                var header = new VisualElement();
                header.AddToClassList("card-header");
                
                string titleText = string.IsNullOrEmpty(icon) ? title : $"{icon} {title}";
                var titleLabel = new Label(titleText);
                titleLabel.AddToClassList("card-title");
                header.Add(titleLabel);
                
                card.Add(header);
            }
            
            body = new VisualElement();
            body.AddToClassList("card-body");
            card.Add(body);
            
            return (card, body);
        }

        #endregion

        #region 提示框

        /// <summary>
        /// 创建帮助提示框
        /// </summary>
        public static VisualElement CreateHelpBox(string message)
        {
            var box = new VisualElement();
            box.AddToClassList("help-box");
            
            var text = new Label(message);
            text.AddToClassList("help-box-text");
            box.Add(text);
            
            return box;
        }

        /// <summary>
        /// 创建空状态提示
        /// </summary>
        public static VisualElement CreateEmptyState(string icon, string message, string hint = null)
        {
            var container = new VisualElement();
            container.AddToClassList("empty-state");
            
            var iconLabel = new Label(icon);
            iconLabel.AddToClassList("empty-state-icon");
            container.Add(iconLabel);
            
            var text = new Label(message);
            text.AddToClassList("empty-state-text");
            container.Add(text);
            
            if (!string.IsNullOrEmpty(hint))
            {
                var hintLabel = new Label(hint);
                hintLabel.AddToClassList("empty-state-hint");
                container.Add(hintLabel);
            }
            
            return container;
        }

        #endregion

        #region 徽章与标签

        /// <summary>
        /// 创建类型徽章
        /// </summary>
        public static Label CreateTypeBadge(string text, Color bgColor)
        {
            var badge = new Label(text);
            badge.style.paddingLeft = Spacing.SM;
            badge.style.paddingRight = Spacing.SM;
            badge.style.paddingTop = 2;
            badge.style.paddingBottom = 2;
            badge.style.backgroundColor = new StyleColor(bgColor);
            badge.style.borderTopLeftRadius = Radius.SM;
            badge.style.borderTopRightRadius = Radius.SM;
            badge.style.borderBottomLeftRadius = Radius.SM;
            badge.style.borderBottomRightRadius = Radius.SM;
            badge.style.fontSize = 10;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            return badge;
        }

        /// <summary>
        /// 创建状态指示点
        /// </summary>
        public static VisualElement CreateStatusDot(Color color, float size = 8f)
        {
            var dot = new VisualElement();
            dot.style.width = size;
            dot.style.height = size;
            dot.style.borderTopLeftRadius = size / 2;
            dot.style.borderTopRightRadius = size / 2;
            dot.style.borderBottomLeftRadius = size / 2;
            dot.style.borderBottomRightRadius = size / 2;
            dot.style.backgroundColor = new StyleColor(color);
            return dot;
        }

        #endregion

        #region 进度条

        /// <summary>
        /// 创建进度条
        /// </summary>
        public static (VisualElement container, VisualElement fill) CreateProgressBar(float initialProgress = 0f)
        {
            var container = new VisualElement();
            container.AddToClassList("progress-bar");
            
            var fill = new VisualElement();
            fill.AddToClassList("progress-bar-fill");
            fill.style.width = Length.Percent(Mathf.Clamp01(initialProgress) * 100);
            container.Add(fill);
            
            return (container, fill);
        }

        #endregion
    }
}
#endif
