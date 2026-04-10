#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Code example rendering helpers for the documentation page.
    /// </summary>
    public partial class DocumentationToolPage
    {
        private const int CODE_FONT_SIZE = 13;

        /// <summary>
        /// Creates the full visual block for one code example.
        /// </summary>
        private VisualElement CreateCodeExampleElement(CodeExample example, VisualElement rootContainer)
        {
            if (example == null)
            {
                return new VisualElement();
            }

            string code = example.Code ?? string.Empty;
            var container = new VisualElement();
            container.style.marginBottom = 20;
            container.style.marginLeft = 18;

            if (!string.IsNullOrEmpty(example.Title))
            {
                container.Add(CreateExampleTitleBar(example.Title));
            }

            var codeContainer = CreateCodeContainer();
            codeContainer.Add(CreateCodeHeader(code, rootContainer));
            codeContainer.Add(CreateCodeBlock(code));
            container.Add(codeContainer);

            if (!string.IsNullOrEmpty(example.Explanation))
            {
                container.Add(CreateExplanationBox(example.Explanation));
            }

            return container;
        }

        /// <summary>
        /// Creates the example title bar shown above a code block.
        /// </summary>
        private VisualElement CreateExampleTitleBar(string title)
        {
            var titleBar = CreateSectionHeader(title, KitIcons.DOT);
            titleBar.style.marginBottom = 8;

            var dot = titleBar.Q<Image>();
            if (dot != null)
            {
                dot.style.width = 8;
                dot.style.height = 8;
                dot.tintColor = Theme.AccentGreen;
                dot.style.marginRight = 8;
            }

            var titleLabel = titleBar.Q<Label>();
            if (titleLabel != null)
            {
                titleLabel.style.fontSize = 13;
                titleLabel.style.color = new StyleColor(Theme.TextSecondary);
            }

            return titleBar;
        }

        /// <summary>
        /// Creates the outer code container.
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
        /// Creates the code block header with language and copy button.
        /// </summary>
        private VisualElement CreateCodeHeader(string code, VisualElement rootContainer)
        {
            var langLabel = new Label("C#");
            langLabel.style.fontSize = 11;
            langLabel.style.color = new StyleColor(Theme.TextDim);

            var copyButton = new Button(() => CopyToClipboard(code, rootContainer));
            copyButton.style.flexDirection = FlexDirection.Row;
            copyButton.style.alignItems = Align.Center;
            copyButton.style.fontSize = 11;
            copyButton.style.paddingLeft = 8;
            copyButton.style.paddingRight = 8;
            copyButton.style.paddingTop = 4;
            copyButton.style.paddingBottom = 4;
            copyButton.style.borderTopLeftRadius = 4;
            copyButton.style.borderTopRightRadius = 4;
            copyButton.style.borderBottomLeftRadius = 4;
            copyButton.style.borderBottomRightRadius = 4;
            copyButton.style.backgroundColor = new StyleColor(Theme.BgHover);
            copyButton.style.borderLeftWidth = 0;
            copyButton.style.borderRightWidth = 0;
            copyButton.style.borderTopWidth = 0;
            copyButton.style.borderBottomWidth = 0;
            copyButton.style.color = new StyleColor(Theme.TextMuted);

            var copyIcon = new Image { image = KitIcons.GetTexture(KitIcons.COPY) };
            copyIcon.style.width = 12;
            copyIcon.style.height = 12;
            copyIcon.style.marginRight = 4;
            copyButton.Add(copyIcon);
            copyButton.Add(new Label("Copy"));

            var codeHeader = CreateHeaderRow(langLabel, copyButton);
            codeHeader.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.08f));
            codeHeader.style.paddingLeft = 16;
            codeHeader.style.paddingRight = 8;
            codeHeader.style.paddingTop = 6;
            codeHeader.style.paddingBottom = 6;
            codeHeader.style.borderBottomWidth = 1;
            codeHeader.style.borderBottomColor = new StyleColor(Theme.Border);
            return codeHeader;
        }

        /// <summary>
        /// Creates the code block body with syntax highlighting.
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
            CreateCodeBlockForUnity2021(codeBlock, code, highlightedCode);
#endif

            return codeBlock;
        }

#if !UNITY_2022_1_OR_NEWER
        /// <summary>
        /// Unity 2021.3 fallback implementation that preserves text selection.
        /// </summary>
        private void CreateCodeBlockForUnity2021(VisualElement codeBlock, string code, string highlightedCode)
        {
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
        /// Creates the explanatory callout shown below a code example.
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

        /// <summary>
        /// Copies code to the system clipboard and shows a toast message.
        /// </summary>
        private void CopyToClipboard(string text, VisualElement rootContainer)
        {
            EditorGUIUtility.systemCopyBuffer = text;

            if (rootContainer != null)
            {
                YokiFrameUIComponents.ShowToast(rootContainer, "Copied to clipboard", 1500, KitIcons.SUCCESS);
            }
        }
    }
}
#endif
