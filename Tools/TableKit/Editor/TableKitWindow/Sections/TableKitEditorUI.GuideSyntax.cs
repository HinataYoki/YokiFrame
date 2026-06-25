#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT

namespace YokiFrame.Unity
{
    /// <summary>
    /// TableKitEditorUI - 语法高亮与代码常量
    /// </summary>
    public partial class TableKitEditorUI
    {
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

        private static readonly string[] sCsharpKeywords =
        {
            "public", "private", "protected", "internal", "static", "readonly", "const",
            "class", "struct", "interface", "enum", "namespace", "using", "new", "return",
            "if", "else", "for", "foreach", "while", "do", "switch", "case", "break",
            "continue", "default", "try", "catch", "finally", "throw", "var", "void",
            "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint",
            "long", "ulong", "short", "ushort", "string", "object", "null", "true", "false",
            "this", "base", "virtual", "override", "abstract", "sealed", "partial", "async", "await"
        };

        private static readonly string[] sCsharpTypes =
        {
            "TableKit", "Tables", "ResKit", "IResourceProvider", "ResourcePackage", "YooAsset",
            "UniTask", "CancellationToken"
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
            foreach (var kw in sCsharpKeywords)
            {
                if (word == kw)
                {
                    result.Append($"<color={SyntaxColors.KEYWORD}>{word}</color>");
                    return;
                }
            }

            // 类型
            foreach (var type in sCsharpTypes)
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
"// 同步访问配置表（首次访问自动调用 Init）\n" +
"var tables = TableKit.Tables;\n" +
"\n" +
"// 编辑器访问配置表\n" +
"#if UNITY_EDITOR\n" +
"var editorTables = TableKit.TablesEditor;\n" +
"#endif";

        private const string GUIDE_CODE_CUSTOM_LOADER =
@"// 覆盖二进制加载
TableKit.SetBinaryLoader(fileName =>
{
    var path = string.Format(TableKit.RuntimePathPattern, fileName);
    return YourLoadBytes(path);
});

// 覆盖 JSON 加载
TableKit.SetJsonLoader(fileName =>
{
    var path = string.Format(TableKit.RuntimePathPattern, fileName);
    return YourLoadText(path);
});";

        private const string GUIDE_CODE_YOOASSET =
@"// TableKit 默认会调用 ResKit
var bytes = YokiFrame.ResKit.LoadRaw(""Art/Table/tb_item"");
var json = YokiFrame.ResKit.LoadRawText(""Art/Table/tb_item"");

// 切换 Unity/Godot/YooAsset 等资源后端时，只替换 ResKit Provider。
YokiFrame.ResKit.SetProvider(new YourResourceProvider());";

        private static readonly string[] sGuideNotes =
        {
            "• 运行时模式路径需与数据输出路径对应",
            "• 默认运行时加载统一走 ResKit.LoadRaw/LoadRawText",
            "• Unity/Godot/YooAsset 等差异应由 ResKit Provider 处理",
            "• 编辑器数据路径默认跟随数据输出目录"
        };

        private const string GUIDE_CODE_ASYNC =
"// 异步初始化（需开启「异步加载模式」并安装 UniTask）\n" +
"// 先异步加载所有表数据到缓存，再同步构造 Tables\n" +
"await TableKit.InitAsync(destroyCancellationToken);\n" +
"\n" +
"// 自定义异步加载器（在 InitAsync 之前调用）\n" +
"TableKit.SetAsyncBinaryLoader(async (fileName, ct) =>\n" +
"{\n" +
"    return await YourAsyncLoadMethod(fileName, ct);\n" +
"});\n" +
"\n" +
"// 覆盖表文件名列表（可选，默认使用生成时嵌入的列表）\n" +
"TableKit.SetTableFileNames(new[] { \"tb_item\", \"tb_config\" });\n" +
"\n" +
"// 异步重新加载（热更新后使用）\n" +
"await TableKit.ReloadAsync(destroyCancellationToken);\n" +
"\n" +
"// 注意：如果未调用 InitAsync，首次访问 TableKit.Tables\n" +
"// 将自动触发同步 Init() 加载";

        #endregion
    }
}
#endif
