namespace YokiFrame
{
    /// <summary>
    /// 表示传统块级 C# namespace 作用域。
    /// </summary>
    public sealed class NamespaceCodeScope : ICodeContainer, ICodeNode
    {
        private readonly string mNamespaceName;
        private readonly CodeScopeBody mBody = new CodeScopeBody();

        /// <summary>
        /// 创建经过限定名称校验的命名空间作用域。
        /// </summary>
        /// <param name="namespaceName">命名空间限定名称。</param>
        internal NamespaceCodeScope(string namespaceName)
        {
            mNamespaceName = CSharpIdentifierValidator.RequireQualifiedName(namespaceName, nameof(namespaceName));
        }

        /// <summary>
        /// 仅允许同一程序集中的 fluent API 追加命名空间成员。
        /// </summary>
        /// <param name="node">待追加节点。</param>
        void ICodeContainer.Add(ICodeNode node)
        {
            mBody.Add(node);
        }

        /// <summary>
        /// 渲染块级 namespace 和内部成员。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        void ICodeNode.Generate(CodeTextWriter writer)
        {
            CodeScopeRenderer.Generate(writer, "namespace " + mNamespaceName, mBody, false);
        }
    }
}
