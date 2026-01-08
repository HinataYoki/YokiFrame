#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - 使用指南区块
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region 使用指南

        private VisualElement mGuideFoldout;

        /// <summary>
        /// 构建使用指南区块（可折叠）
        /// </summary>
        private VisualElement BuildUsageGuide()
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(Design.LayerCard);
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 8;
            container.style.borderLeftWidth = container.style.borderRightWidth = 1;
            container.style.borderTopWidth = container.style.borderBottomWidth = 1;
            container.style.borderLeftColor = container.style.borderRightColor = new StyleColor(Design.BorderDefault);
            container.style.borderTopColor = container.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.style.marginBottom = 16;

            // 折叠头部
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.cursor = StyleKeyword.Initial;
            container.Add(header);

            var arrow = new Image { name = "guide-foldout-arrow", image = TableKitIcons.GetIcon(TableKitIcons.CHEVRON_RIGHT) };
            arrow.style.width = 12;
            arrow.style.height = 12;
            arrow.style.marginRight = 6;
            header.Add(arrow);

            var title = new Label("使用指南");
            title.style.fontSize = Design.FontSizeSection;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Design.TextPrimary);
            title.style.flexGrow = 1;
            header.Add(title);

            // 折叠内容
            bool isExpanded = EditorPrefs.GetBool(PREF_GUIDE_EXPANDED, false);
            mGuideFoldout = new VisualElement();
            mGuideFoldout.style.paddingLeft = 12;
            mGuideFoldout.style.paddingRight = 12;
            mGuideFoldout.style.paddingBottom = 12;
            mGuideFoldout.style.borderTopWidth = 1;
            mGuideFoldout.style.borderTopColor = new StyleColor(Design.BorderDefault);
            mGuideFoldout.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            container.Add(mGuideFoldout);

            arrow.image = isExpanded ? TableKitIcons.GetIcon(TableKitIcons.CHEVRON_DOWN) : TableKitIcons.GetIcon(TableKitIcons.CHEVRON_RIGHT);

            header.RegisterCallback<ClickEvent>(_ =>
            {
                bool expanded = mGuideFoldout.style.display == DisplayStyle.Flex;
                mGuideFoldout.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
                arrow.image = expanded ? TableKitIcons.GetIcon(TableKitIcons.CHEVRON_RIGHT) : TableKitIcons.GetIcon(TableKitIcons.CHEVRON_DOWN);
                EditorPrefs.SetBool(PREF_GUIDE_EXPANDED, !expanded);
            });

            BuildGuideContent(mGuideFoldout);
            return container;
        }

        /// <summary>
        /// 构建使用指南内容
        /// </summary>
        private void BuildGuideContent(VisualElement container)
        {
            // 基础用法
            var basicSection = CreateGuideSection("基础用法 (Resources 加载)");
            basicSection.style.marginTop = 12;
            container.Add(basicSection);

            AddGuideDescription(basicSection, "TableKit 默认使用 Resources.Load 加载配置数据，无需额外配置：");
            basicSection.Add(CreateCodeBlock(GUIDE_CODE_BASIC));

            // 自定义加载器
            var customSection = CreateGuideSection("自定义加载器");
            container.Add(customSection);

            AddGuideDescription(customSection, "如需使用 Addressables 或 YooAsset 等资源管理方案，可实现自定义加载器：");
            customSection.Add(CreateCodeBlock(GUIDE_CODE_CUSTOM_LOADER));

            // YooAsset 示例
            var yooSection = CreateGuideSection("YooAsset 加载器示例");
            container.Add(yooSection);

            AddGuideDescription(yooSection, "使用 YooAsset 加载配置表的完整实现：");
            yooSection.Add(CreateCodeBlock(GUIDE_CODE_YOOASSET));

            // 注意事项
            var noteSection = CreateGuideSection("注意事项");
            container.Add(noteSection);

            foreach (var note in GUIDE_NOTES)
            {
                var noteLabel = new Label(note);
                noteLabel.style.color = new StyleColor(Design.TextSecondary);
                noteLabel.style.fontSize = Design.FontSizeBody;
                noteLabel.style.marginTop = 4;
                noteSection.Add(noteLabel);
            }
        }

        private void AddGuideDescription(VisualElement parent, string text)
        {
            var desc = new Label(text);
            desc.style.color = new StyleColor(Design.TextSecondary);
            desc.style.fontSize = Design.FontSizeBody;
            desc.style.marginBottom = 8;
            desc.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(desc);
        }

        private VisualElement CreateGuideSection(string title)
        {
            var section = new VisualElement();
            section.style.marginBottom = 12;

            var titleLabel = new Label(title);
            titleLabel.style.color = new StyleColor(Design.BrandPrimary);
            titleLabel.style.fontSize = Design.FontSizeBody;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 6;
            section.Add(titleLabel);

            return section;
        }

        private VisualElement CreateCodeBlock(string code)
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(Design.LayerConsole);
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 4;
            container.style.paddingLeft = 8;
            container.style.paddingRight = 8;
            container.style.paddingTop = 6;
            container.style.paddingBottom = 6;
            container.style.overflow = Overflow.Hidden;

            var scrollView = new ScrollView(ScrollViewMode.Horizontal);
            scrollView.style.flexGrow = 1;
            container.Add(scrollView);

            var highlightedCode = ApplySyntaxHighlighting(code);
            var codeLabel = new Label(highlightedCode);
            codeLabel.style.fontSize = Design.FontSizeCode;
#if UNITY_6000_0_OR_NEWER
            codeLabel.style.whiteSpace = WhiteSpace.PreWrap;
#else
            codeLabel.style.whiteSpace = WhiteSpace.Normal;
#endif
            codeLabel.enableRichText = true;
            scrollView.Add(codeLabel);

            return container;
        }

        #endregion
    }
}
#endif
