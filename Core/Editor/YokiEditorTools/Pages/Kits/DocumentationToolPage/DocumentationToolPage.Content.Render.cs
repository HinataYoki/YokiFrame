#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 文档页面 - 代码示例渲染
    /// </summary>
    public partial class DocumentationToolPage
    {
        #region 常量

        /// <summary>
        /// 代码字体大小
        /// </summary>
        private const int CODE_FONT_SIZE = 13;

        #endregion

        #region 代码示例渲染

        /// <summary>
        /// 创建代码示例元素
        /// </summary>
        private VisualElement CreateCodeExampleElement(CodeExample example, VisualElement rootContainer)
        {
            var container = new VisualElement();
            container.style.marginBottom = 20;
            container.style.marginLeft = 18;

            // 示例标题栏
            if (!string.IsNullOrEmpty(example.Title))
            {
                container.Add(CreateExampleTitleBar(example.Title));
            }

            // 代码块容器
            var codeContainer = CreateCodeContainer();

            // 代码块头部
            codeContainer.Add(CreateCodeHeader(example.Code, rootContainer));

            // 代码内容
            codeContainer.Add(CreateCodeBlock(example.Code));

            container.Add(codeContainer);

            // 说明提示框
            if (!string.IsNullOrEmpty(example.Explanation))
            {
                container.Add(CreateExplanationBox(example.Explanation));
            }

            return container;
        }

        /// <summary>
        /// 创建示例标题栏
        /// </summary>
        private VisualElement CreateExampleTitleBar(string title)
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

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 13;
            titleLabel.style.color = new StyleColor(Theme.TextSecondary);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleBar.Add(titleLabel);

            return titleBar;
        }

        /// <summary>
        /// 创建代码容器
        /// </summary>
        private VisualElement CreateCodeContainer()
        {
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
            return codeContainer;
        }

        /// <summary>
        /// 创建代码块头部
        /// </summary>
        private VisualElement CreateCodeHeader(string code, VisualElement rootContainer)
        {
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
            var copyBtn = new Button(() => CopyToClipboard(code, rootContainer));
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
            return codeHeader;
        }

        /// <summary>
        /// 创建代码块内容
        /// </summary>
        private VisualElement CreateCodeBlock(string code)
        {
            var codeBlock = new VisualElement();
            codeBlock.style.backgroundColor = new StyleColor(Theme.BgCode);
            codeBlock.style.paddingLeft = 16;
            codeBlock.style.paddingRight = 16;
            codeBlock.style.paddingTop = 14;
            codeBlock.style.paddingBottom = 14;
            codeBlock.style.position = Position.Relative;

            var highlightedCode = CSharpSyntaxHighlighter.Highlight(code, 0);

#if UNITY_2022_1_OR_NEWER
            // Unity 2022.1+ 支持 Label 的文本选择功能，单层即可
            var codeLabel = new Label();
            codeLabel.enableRichText = true;
            codeLabel.text = highlightedCode;
            codeLabel.style.fontSize = CODE_FONT_SIZE;
            codeLabel.style.unityParagraphSpacing = 0;
            codeLabel.style.marginTop = 0;
            codeLabel.style.marginBottom = 0;
            codeLabel.style.paddingTop = 0;
            codeLabel.style.paddingBottom = 0;
            codeLabel.selection.isSelectable = true;
#if UNITY_6000_0_OR_NEWER
            codeLabel.style.whiteSpace = WhiteSpace.Pre;
#else
            codeLabel.style.whiteSpace = WhiteSpace.NoWrap;
#endif
            codeBlock.Add(codeLabel);
#else
            // Unity 2021.3 使用双层方案：语法高亮 Label + 透明 TextField
            CreateCodeBlockForUnity2021(codeBlock, code, highlightedCode);
#endif

            return codeBlock;
        }

#if !UNITY_2022_1_OR_NEWER
        /// <summary>
        /// Unity 2021.3 的代码块实现（双层方案）
        /// </summary>
        private void CreateCodeBlockForUnity2021(VisualElement codeBlock, string code, string highlightedCode)
        {
            // 底层：语法高亮的 Label（用于显示）
            var codeLabel = new Label();
            codeLabel.enableRichText = true;
            codeLabel.text = highlightedCode;
            codeLabel.style.fontSize = CODE_FONT_SIZE;
            codeLabel.style.unityParagraphSpacing = 0;
            codeLabel.style.marginTop = 0;
            codeLabel.style.marginBottom = 0;
            codeLabel.style.paddingTop = 0;
            codeLabel.style.paddingBottom = 0;
            codeLabel.style.whiteSpace = WhiteSpace.NoWrap;
            codeLabel.pickingMode = PickingMode.Ignore;
            codeBlock.Add(codeLabel);

            // 顶层：透明的 TextField（用于选择复制）
            var codeTextField = new TextField();
            codeTextField.multiline = true;
            codeTextField.isReadOnly = true;
            codeTextField.value = code;
            codeTextField.style.position = Position.Absolute;
            codeTextField.style.left = 0;
            codeTextField.style.right = 0;
            codeTextField.style.top = 0;
            codeTextField.style.bottom = 0;
            codeTextField.style.fontSize = CODE_FONT_SIZE;
            codeTextField.style.marginLeft = 0;
            codeTextField.style.marginRight = 0;
            codeTextField.style.marginTop = 0;
            codeTextField.style.marginBottom = 0;
            codeTextField.style.paddingLeft = 16;
            codeTextField.style.paddingRight = 16;
            codeTextField.style.paddingTop = 14;
            codeTextField.style.paddingBottom = 14;
            codeTextField.style.backgroundColor = new StyleColor(Color.clear);
            codeTextField.style.borderLeftWidth = 0;
            codeTextField.style.borderRightWidth = 0;
            codeTextField.style.borderTopWidth = 0;
            codeTextField.style.borderBottomWidth = 0;
            codeTextField.style.whiteSpace = WhiteSpace.NoWrap;
            codeTextField.style.unityParagraphSpacing = 0;
            // 文字几乎透明，只保留选择功能
            codeTextField.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.01f));

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
                textInput.style.marginLeft = 0;
                textInput.style.marginRight = 0;
                textInput.style.marginTop = 0;
                textInput.style.marginBottom = 0;
                textInput.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.01f));
                textInput.style.unityParagraphSpacing = 0;
                textInput.style.fontSize = CODE_FONT_SIZE;
                textInput.style.whiteSpace = WhiteSpace.NoWrap;
            }
            codeBlock.Add(codeTextField);
        }
#endif

        /// <summary>
        /// 创建说明提示框
        /// </summary>
        private VisualElement CreateExplanationBox(string explanation)
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

            var explanationLabel = new Label(explanation);
            explanationLabel.style.fontSize = 14;
            explanationLabel.style.color = new StyleColor(new Color(0.9f, 0.85f, 0.7f));
            explanationLabel.style.whiteSpace = WhiteSpace.Normal;
            explanationLabel.style.flexShrink = 1;
            explanationBox.Add(explanationLabel);

            return explanationBox;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 复制到剪贴板
        /// </summary>
        private void CopyToClipboard(string text, VisualElement rootContainer)
        {
            EditorGUIUtility.systemCopyBuffer = text;

            // 显示 Toast 提示
            if (rootContainer != default)
            {
                YokiFrameUIComponents.ShowToast(rootContainer, "已复制到剪贴板", 1500, KitIcons.SUCCESS);
            }
        }

        #endregion
    }
}
#endif
