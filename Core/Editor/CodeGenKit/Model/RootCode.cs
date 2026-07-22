namespace YokiFrame
{
    /// <summary>
    /// 表示单个生成文件的根作用域，保存 using、命名空间和顶层声明顺序。
    /// </summary>
    public sealed class RootCode : ICodeContainer, ICodeNode
    {
        private readonly CodeScopeBody mBody = new CodeScopeBody();

        /// <summary>
        /// 创建空的代码文件根作用域。
        /// </summary>
        public RootCode()
        {
        }

        /// <summary>
        /// 仅允许同一程序集中的 fluent API 向根作用域追加受控节点。
        /// </summary>
        /// <param name="node">待追加节点。</param>
        void ICodeContainer.Add(ICodeNode node)
        {
            mBody.Add(node);
        }

        /// <summary>
        /// 将根作用域中的全部节点按顺序渲染到源码 writer。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        void ICodeNode.Generate(CodeTextWriter writer)
        {
            mBody.Generate(writer);
        }
    }
}
