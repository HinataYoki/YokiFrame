#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ToolClass SpanSplitter 零分配分割文档
    /// </summary>
    internal static class ToolClassDocSpanSplitter
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "SpanSplitter 零分配分割",
                Description = "使用 Span<char> 实现的字符串分割器，完全避免字符串分配。适合高频字符串处理场景。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "使用示例",
                        Code = @"// 传统方式（产生 GC）
// string[] parts = text.Split(',');

// 零分配方式
var text = ""item1,item2,item3,item4"";
var splitter = new SpanSplitter(text.AsSpan(), ',');

while (splitter.MoveNext(out var part))
{
    // part 是 ReadOnlySpan<char>，不会分配新字符串
    Debug.Log(part.ToString()); // 仅在需要时转换
    
    // 直接比较
    if (part.SequenceEqual(""item2""))
    {
        // 找到了
    }
}",
                        Explanation = "SpanSplitter 是 ref struct，只能在栈上使用，不能作为类成员。"
                    }
                }
            };
        }
    }
}
#endif
