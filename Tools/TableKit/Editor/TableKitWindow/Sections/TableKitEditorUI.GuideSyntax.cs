#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT

namespace YokiFrame.TableKit.Editor
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
"// 运行时访问配置表\n" +
"var tables = TableKit.Tables;\n" +
"var itemConfig = tables.TbItem.Get(1001);\n" +
"\n" +
"// 编辑器访问配置表\n" +
"#if UNITY_EDITOR\n" +
"var editorTables = TableKit.TablesEditor;\n" +
"#endif";

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
