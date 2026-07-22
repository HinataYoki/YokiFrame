namespace YokiFrame
{
    /// <summary>
    /// 统一渲染块级作用域的声明头、花括号、缩进和可选分号。
    /// </summary>
    internal static class CodeScopeRenderer
    {
        /// <summary>
        /// 渲染完整块级作用域，并在子节点失败时恢复 writer 缩进状态。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        /// <param name="firstLine">作用域声明头。</param>
        /// <param name="body">作用域节点集合。</param>
        /// <param name="semicolon">闭合花括号后是否追加分号。</param>
        internal static void Generate(CodeTextWriter writer, string firstLine, CodeScopeBody body, bool semicolon)
        {
            writer.WriteLine(firstLine);
            writer.WriteLine("{");
            writer.PushIndent();
            try
            {
                body.Generate(writer);
            }
            finally
            {
                writer.PopIndent();
            }

            writer.WriteLine(semicolon ? "};" : "}");
        }
    }
}
