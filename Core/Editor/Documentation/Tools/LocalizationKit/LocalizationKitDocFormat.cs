#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LocalizationKit 文本格式化文档
    /// </summary>
    internal static class LocalizationKitDocFormat
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "文本格式化",
                Description = "支持占位符、格式说明符和自定义标签。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "格式化功能",
                        Code = @"// 索引占位符
// 模板: ""你好，{0}！你有 {1} 条消息。""
LocalizationKit.Get(TextId.MSG, ""Alice"", 5);

// 格式说明符
// 模板: ""价格: {0:F2} 元""
LocalizationKit.Get(TextId.PRICE, 19.99f); // ""价格: 19.99 元""

// 转义大括号
// 模板: ""{{0}} 表示占位符""
// 结果: ""{0} 表示占位符""

// 自定义标签处理
var formatter = LocalizationKit.GetFormatter() as DefaultTextFormatter;
formatter.RegisterTagHandler(""item"", param => $""[物品:{param}]"");
// 模板: ""你获得了 <item:1001>""
// 结果: ""你获得了 [物品:1001]""",
                        Explanation = "支持 Unity 原生富文本标签（如 <color>、<b>）和自定义标签。"
                    }
                }
            };
        }
    }
}
#endif
