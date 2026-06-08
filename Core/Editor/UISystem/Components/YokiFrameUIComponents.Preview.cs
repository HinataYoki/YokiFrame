#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - 预览/文件组件
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region 文件预览

        /// <summary>
        /// 创建文件预览行
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="exists">文件是否存在</param>
        /// <param name="onClick">点击回调（可选）</param>
        /// <returns>文件预览行元素</returns>
        public static VisualElement CreateFilePreviewRow(string path, bool exists, Action onClick = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = Spacing.XS;
            row.style.paddingBottom = Spacing.XS;
            row.style.paddingLeft = Spacing.SM;
            row.style.paddingRight = Spacing.SM;

            // 状态图标
            var statusIcon = new Image 
            { 
                image = KitIcons.GetTexture(exists ? KitIcons.DOT_FILLED : KitIcons.DOT_EMPTY) 
            };
            statusIcon.style.width = 12;
            statusIcon.style.height = 12;
            statusIcon.tintColor = exists ? Colors.FileExists : Colors.FileNotExists;
            statusIcon.style.marginRight = Spacing.SM;
            row.Add(statusIcon);

            // 文件路径
            var pathLabel = new Label(path);
            pathLabel.style.fontSize = 11;
            pathLabel.style.color = new StyleColor(exists ? Colors.TextSecondary : Colors.TextTertiary);
            pathLabel.style.flexGrow = 1;
            pathLabel.style.overflow = Overflow.Hidden;
            pathLabel.style.textOverflow = TextOverflow.Ellipsis;
            row.Add(pathLabel);

            // 状态文本
            var statusText = new Label(exists ? "已存在" : "将创建");
            statusText.style.fontSize = 10;
            statusText.style.color = new StyleColor(exists ? Colors.StatusWarning : Colors.StatusSuccess);
            row.Add(statusText);

            // 点击事件
            if (onClick != null)
            {
                row.AddToClassList("clickable");
                row.RegisterCallback<ClickEvent>(_ => onClick());
                row.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    row.style.backgroundColor = new StyleColor(Colors.LayerHover);
                });
                row.RegisterCallback<MouseLeaveEvent>(_ =>
                {
                    row.style.backgroundColor = StyleKeyword.Null;
                });
            }

            return row;
        }

        /// <summary>
        /// 创建文件列表容器
        /// </summary>
        /// <param name="title">标题（可选）</param>
        /// <returns>文件列表容器元素</returns>
        public static VisualElement CreateFileListContainer(string title = null)
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(Colors.LayerCard);
            container.style.borderTopLeftRadius = Radius.MD;
            container.style.borderTopRightRadius = Radius.MD;
            container.style.borderBottomLeftRadius = Radius.MD;
            container.style.borderBottomRightRadius = Radius.MD;
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopColor = new StyleColor(Colors.BorderLight);
            container.style.borderBottomColor = new StyleColor(Colors.BorderLight);
            container.style.borderLeftColor = new StyleColor(Colors.BorderLight);
            container.style.borderRightColor = new StyleColor(Colors.BorderLight);
            container.style.marginTop = Spacing.SM;
            container.style.marginBottom = Spacing.SM;

            if (!string.IsNullOrEmpty(title))
            {
                var header = new VisualElement();
                header.style.paddingLeft = Spacing.SM;
                header.style.paddingRight = Spacing.SM;
                header.style.paddingTop = Spacing.XS;
                header.style.paddingBottom = Spacing.XS;
                header.style.borderBottomWidth = 1;
                header.style.borderBottomColor = new StyleColor(Colors.BorderLight);

                var titleLabel = new Label(title);
                titleLabel.style.fontSize = 11;
                titleLabel.style.color = new StyleColor(Colors.TextSecondary);
                header.Add(titleLabel);

                container.Add(header);
            }

            return container;
        }

        #endregion

        #region 代码预览

        /// <summary>
        /// 创建代码预览区域
        /// </summary>
        /// <param name="code">代码内容</param>
        /// <param name="maxHeight">最大高度（可选）</param>
        /// <returns>代码预览区域元素</returns>
        public static VisualElement CreateCodePreview(string code, float maxHeight = 200)
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f));
            container.style.borderTopLeftRadius = Radius.MD;
            container.style.borderTopRightRadius = Radius.MD;
            container.style.borderBottomLeftRadius = Radius.MD;
            container.style.borderBottomRightRadius = Radius.MD;
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopColor = new StyleColor(Colors.BorderLight);
            container.style.borderBottomColor = new StyleColor(Colors.BorderLight);
            container.style.borderLeftColor = new StyleColor(Colors.BorderLight);
            container.style.borderRightColor = new StyleColor(Colors.BorderLight);
            container.style.paddingLeft = Spacing.SM;
            container.style.paddingRight = Spacing.SM;
            container.style.paddingTop = Spacing.SM;
            container.style.paddingBottom = Spacing.SM;
            container.style.maxHeight = maxHeight;
            container.style.overflow = Overflow.Hidden;

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;

            var codeLabel = new Label(code);
            codeLabel.style.fontSize = 11;
            codeLabel.style.color = new StyleColor(Colors.TextSecondary);
            codeLabel.style.whiteSpace = WhiteSpace.Normal;
            codeLabel.style.unityFontDefinition = new StyleFontDefinition(StyleKeyword.Initial);
            codeLabel.enableRichText = false;
            scrollView.Add(codeLabel);

            container.Add(scrollView);

            return container;
        }

        /// <summary>
        /// 创建带标题的代码预览区域
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="code">代码内容</param>
        /// <param name="maxHeight">最大高度（可选）</param>
        /// <returns>代码预览区域元素</returns>
        public static VisualElement CreateCodePreviewWithTitle(string title, string code, float maxHeight = 200)
        {
            var container = new VisualElement();
            container.style.marginTop = Spacing.SM;
            container.style.marginBottom = Spacing.SM;

            // 标题栏
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.backgroundColor = new StyleColor(Colors.LayerToolbar);
            header.style.paddingLeft = Spacing.SM;
            header.style.paddingRight = Spacing.SM;
            header.style.paddingTop = Spacing.XS;
            header.style.paddingBottom = Spacing.XS;
            header.style.borderTopLeftRadius = Radius.MD;
            header.style.borderTopRightRadius = Radius.MD;

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 11;
            titleLabel.style.color = new StyleColor(Colors.TextSecondary);
            header.Add(titleLabel);

            container.Add(header);

            // 代码区域
            var codeContainer = CreateCodePreview(code, maxHeight);
            codeContainer.style.borderTopLeftRadius = 0;
            codeContainer.style.borderTopRightRadius = 0;
            codeContainer.style.marginTop = 0;
            container.Add(codeContainer);

            return container;
        }

        #endregion

        #region 预览图片

        /// <summary>
        /// 创建图片预览区域
        /// </summary>
        /// <param name="texture">纹理</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <returns>图片预览区域元素</returns>
        public static VisualElement CreateImagePreview(Texture2D texture, float width = 100, float height = 100)
        {
            var container = new VisualElement();
            container.style.width = width;
            container.style.height = height;
            container.style.backgroundColor = new StyleColor(Colors.LayerCard);
            container.style.borderTopLeftRadius = Radius.MD;
            container.style.borderTopRightRadius = Radius.MD;
            container.style.borderBottomLeftRadius = Radius.MD;
            container.style.borderBottomRightRadius = Radius.MD;
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopColor = new StyleColor(Colors.BorderLight);
            container.style.borderBottomColor = new StyleColor(Colors.BorderLight);
            container.style.borderLeftColor = new StyleColor(Colors.BorderLight);
            container.style.borderRightColor = new StyleColor(Colors.BorderLight);
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;

            if (texture != null)
            {
                var image = new Image { image = texture };
                image.scaleMode = ScaleMode.ScaleToFit;
                image.style.width = width - 4;
                image.style.height = height - 4;
                container.Add(image);
            }
            else
            {
                var placeholder = new Label("无预览");
                placeholder.style.fontSize = 11;
                placeholder.style.color = new StyleColor(Colors.TextTertiary);
                container.Add(placeholder);
            }

            return container;
        }

        #endregion
    }
}
#endif
