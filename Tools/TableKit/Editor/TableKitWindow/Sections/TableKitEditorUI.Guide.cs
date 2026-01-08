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

            var arrow = new Label("▶") { name = "guide-foldout-arrow" };
            arrow.style.fontSize = Design.FontSizeSmall;
            arrow.style.color = new StyleColor(Design.TextTertiary);
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

            arrow.text = isExpanded ? "▼" : "▶";

            header.RegisterCallback<ClickEvent>(_ =>
            {
                bool expanded = mGuideFoldout.style.display == DisplayStyle.Flex;
                mGuideFoldout.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
                arrow.text = expanded ? "▶" : "▼";
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
            codeLabel.style.whiteSpace = WhiteSpace.PreWrap;
            codeLabel.enableRichText = true;
            scrollView.Add(codeLabel);

            return container;
        }

        #endregion

        #region 语法高亮

        /// <summary>
        /// 语法高亮颜色常量
        /// </summary>
        private static class SyntaxColors
        {
            public const string KEYWORD = "#569CD6";
            public const string TYPE = "#4EC9B0";
            public const string STRING = "#CE9178";
            public const string COMMENT = "#6A9955";
            public const string NUMBER = "#B5CEA8";
            public const string METHOD = "#DCDCAA";
            public const string DEFAULT = "#D4D4D4";
        }

        private static readonly string[] CSHARP_KEYWORDS =
        {
            "public", "private", "protected", "internal", "static", "readonly", "const",
            "class", "struct", "interface", "enum", "namespace", "using", "new", "return",
            "if", "else", "for", "foreach", "while", "do", "switch", "case", "break",
            "continue", "default", "try", "catch", "finally", "throw", "var", "void",
            "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint",
            "long", "ulong", "short", "ushort", "string", "object", "null", "true", "false",
            "this", "base", "virtual", "override", "abstract", "sealed", "partial", "async", "await"
        };

        private static readonly string[] CSHARP_TYPES =
        {
            "TableKit", "Tables", "ITableLoader", "ResourcePackage", "YooAssets", "YooAsset"
        };

        private string ApplySyntaxHighlighting(string code)
        {
            var result = new System.Text.StringBuilder(code.Length * 2);
            var lines = code.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) result.Append('\n');
                result.Append(HighlightLine(lines[i]));
            }

            return result.ToString();
        }

        private string HighlightLine(string line)
        {
            // 注释处理
            var commentIndex = line.IndexOf("//");
            if (commentIndex >= 0)
            {
                var beforeComment = line.Substring(0, commentIndex);
                var comment = line.Substring(commentIndex);
                return HighlightTokens(beforeComment) + $"<color={SyntaxColors.COMMENT}>{EscapeRichText(comment)}</color>";
            }

            return HighlightTokens(line);
        }

        private string HighlightTokens(string text)
        {
            var result = new System.Text.StringBuilder();
            var token = new System.Text.StringBuilder();
            bool inString = false;
            char stringChar = '"';

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                // 字符串处理
                if ((c == '"' || c == '\'') && (i == 0 || text[i - 1] != '\\'))
                {
                    if (!inString)
                    {
                        FlushToken(result, token);
                        inString = true;
                        stringChar = c;
                        token.Append(c);
                    }
                    else if (c == stringChar)
                    {
                        token.Append(c);
                        result.Append($"<color={SyntaxColors.STRING}>{EscapeRichText(token.ToString())}</color>");
                        token.Clear();
                        inString = false;
                    }
                    else
                    {
                        token.Append(c);
                    }
                    continue;
                }

                if (inString)
                {
                    token.Append(c);
                    continue;
                }

                // 标识符边界
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    FlushToken(result, token);
                    result.Append(c);
                }
                else
                {
                    token.Append(c);
                }
            }

            FlushToken(result, token);
            return result.ToString();
        }

        private void FlushToken(System.Text.StringBuilder result, System.Text.StringBuilder token)
        {
            if (token.Length == 0) return;

            var word = token.ToString();
            token.Clear();

            // 数字
            if (char.IsDigit(word[0]))
            {
                result.Append($"<color={SyntaxColors.NUMBER}>{word}</color>");
                return;
            }

            // 关键字
            foreach (var kw in CSHARP_KEYWORDS)
            {
                if (word == kw)
                {
                    result.Append($"<color={SyntaxColors.KEYWORD}>{word}</color>");
                    return;
                }
            }

            // 类型
            foreach (var type in CSHARP_TYPES)
            {
                if (word == type)
                {
                    result.Append($"<color={SyntaxColors.TYPE}>{word}</color>");
                    return;
                }
            }

            result.Append(word);
        }

        private static string EscapeRichText(string text) =>
            text.Replace("<", "\\<").Replace(">", "\\>");

        #endregion

        #region 指南代码常量

        private const string GUIDE_CODE_BASIC =
@"// 运行时访问配置表
var tables = TableKit.Tables;
var itemConfig = tables.TbItem.Get(1001);

// 编辑器访问配置表
#if UNITY_EDITOR
var editorTables = TableKit.TablesEditor;
#endif";

        private const string GUIDE_CODE_CUSTOM_LOADER =
@"// 实现 ITableLoader 接口
public class MyTableLoader : ITableLoader
{
    public byte[] Load(string tableName)
    {
        // 自定义加载逻辑
        return yourLoadMethod(tableName);
    }
}

// 初始化时设置加载器
TableKit.SetLoader(new MyTableLoader());";

        private const string GUIDE_CODE_YOOASSET =
@"using YooAsset;

public class YooAssetTableLoader : ITableLoader
{
    private readonly ResourcePackage mPackage;
    private readonly string mPathPattern;

    public YooAssetTableLoader(ResourcePackage package, string pathPattern = ""{0}"")
    {
        mPackage = package;
        mPathPattern = pathPattern;
    }

    public byte[] Load(string tableName)
    {
        var path = string.Format(mPathPattern, tableName);
        var handle = mPackage.LoadRawFileSync(path);
        return handle.GetRawFileData();
    }
}

// 使用示例
var package = YooAssets.GetPackage(""DefaultPackage"");
TableKit.SetLoader(new YooAssetTableLoader(package, ""Art/Table/{0}""));";

        private static readonly string[] GUIDE_NOTES =
        {
            "• 运行时模式路径需与数据输出路径对应",
            "• 使用 Resources 时，数据需放在 Resources 文件夹下",
            "• 使用 YooAsset 时，确保资源已正确打包",
            "• 编辑器数据路径用于 TableKit.TablesEditor 访问"
        };

        #endregion
    }
}
#endif
