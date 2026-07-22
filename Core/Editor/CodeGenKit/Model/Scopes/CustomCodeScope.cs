namespace YokiFrame
{
    /// <summary>
    /// 表示由调用方提供声明头的通用块级作用域，例如 if、switch 或访问器。
    /// </summary>
    public sealed class CustomCodeScope : ICodeContainer, ICodeNode
    {
        private readonly string mFirstLine;
        private readonly bool mSemicolon;
        private readonly CodeScopeBody mBody = new CodeScopeBody();

        /// <summary>
        /// 创建自定义块级作用域，声明头必须保持为单行代码。
        /// </summary>
        /// <param name="firstLine">花括号前的声明头。</param>
        /// <param name="semicolon">闭合花括号后是否追加分号。</param>
        internal CustomCodeScope(string firstLine, bool semicolon)
        {
            mFirstLine = CSharpText.RequireNonEmptyLine(firstLine, nameof(firstLine));
            mSemicolon = semicolon;
        }

        /// <summary>
        /// 仅允许同一程序集中的 fluent API 追加自定义作用域内容。
        /// </summary>
        /// <param name="node">待追加节点。</param>
        void ICodeContainer.Add(ICodeNode node)
        {
            mBody.Add(node);
        }

        /// <summary>
        /// 渲染调用方声明头、花括号和内部节点。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        void ICodeNode.Generate(CodeTextWriter writer)
        {
            CodeScopeRenderer.Generate(writer, mFirstLine, mBody, mSemicolon);
        }
    }
}
