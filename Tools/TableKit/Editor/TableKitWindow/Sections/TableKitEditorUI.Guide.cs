#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.Unity
{
    /// <summary>
    /// TableKitEditorUI - 使用指南区块
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region 使用指南

        private const string PREF_GUIDE_EXPANDED = "TableKit_GuideExpanded";

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
            title.style.fontSize = Design.FONT_SIZE_SECTION;
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
            var basicSection = CreateGuideSection("基础用法 (ResKit 加载)");
            basicSection.style.marginTop = 12;
            container.Add(basicSection);

            AddGuideDescription(basicSection, "TableKit 默认通过 ResKit 加载配置数据，引擎差异由 ResKit Provider 处理：");
            basicSection.Add(CreateCodeBlock(GUIDE_CODE_BASIC));

            // 自定义加载器
            var customSection = CreateGuideSection("自定义加载器");
            container.Add(customSection);

            AddGuideDescription(customSection, "如需覆盖默认资源读取，可注入二进制或 JSON 加载函数：");
            customSection.Add(CreateCodeBlock(GUIDE_CODE_CUSTOM_LOADER));

            // ResKit Provider 示例
            var yooSection = CreateGuideSection("ResKit 后端示例");
            container.Add(yooSection);

            AddGuideDescription(yooSection, "YooAsset、Resources、Godot FileAccess 等差异应放在 ResKit Provider 内，TableKit 无需再区分引擎：");
            yooSection.Add(CreateCodeBlock(GUIDE_CODE_YOOASSET));

            // 异步加载
            var asyncSection = CreateGuideSection("异步加载模式");
            container.Add(asyncSection);

            AddGuideDescription(asyncSection,
                "开启构建选项中的「异步加载模式」后，生成的代码包含 InitAsync 异步初始化方法。" +
                "可通过 SetAsyncBinaryLoader/SetAsyncJsonLoader 自定义异步加载方式，" +
                "通过 SetTableFileNames 覆盖预加载的文件列表。" +
                "如果不显式调用 InitAsync，首次访问 TableKit.Tables 时将自动触发同步 Init() 加载：");
            asyncSection.Add(CreateCodeBlock(GUIDE_CODE_ASYNC));

            // 注意事项
            var noteSection = CreateGuideSection("注意事项");
            container.Add(noteSection);

            foreach (var note in sGuideNotes)
            {
                var noteLabel = new Label(note);
                noteLabel.style.color = new StyleColor(Design.TextSecondary);
                noteLabel.style.fontSize = Design.FONT_SIZE_BODY;
                noteLabel.style.marginTop = 4;
                noteSection.Add(noteLabel);
            }
        }

        private void AddGuideDescription(VisualElement parent, string text)
        {
            var desc = new Label(text);
            desc.style.color = new StyleColor(Design.TextSecondary);
            desc.style.fontSize = Design.FONT_SIZE_BODY;
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
            titleLabel.style.fontSize = Design.FONT_SIZE_BODY;
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
            codeLabel.style.fontSize = Design.FONT_SIZE_CODE;
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
