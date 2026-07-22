namespace YokiFrame
{
    /// <summary>
    /// 表示 CodeGenKit 支持的注释节点类型。
    /// </summary>
    public enum CommentType
    {
        SingleLine,
        XmlSummary,
        XmlParam,
        XmlReturns
    }

    /// <summary>
    /// 表示普通注释或 XML 文档注释节点。
    /// </summary>
    internal sealed class CommentCode : ICodeNode
    {
        private readonly string mContent;
        private readonly CommentType mType;
        private readonly string mParameterName;

        /// <summary>
        /// 创建指定类型的注释节点，并提前验证 XML 参数名称。
        /// </summary>
        /// <param name="content">注释正文；null 按空文本处理。</param>
        /// <param name="type">注释类型。</param>
        /// <param name="parameterName">XmlParam 对应的参数名。</param>
        internal CommentCode(string content, CommentType type, string parameterName = null)
        {
            mContent = content ?? string.Empty;
            mType = type;
            mParameterName = type == CommentType.XmlParam
                ? CSharpIdentifierValidator.RequireIdentifier(parameterName, nameof(parameterName))
                : parameterName;
        }

        /// <summary>
        /// 按注释类型选择普通注释或统一 XML 文档 writer。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        public void Generate(CodeTextWriter writer)
        {
            switch (mType)
            {
                case CommentType.XmlSummary:
                    XmlDocumentationWriter.WriteSummary(writer, mContent);
                    break;
                case CommentType.XmlParam:
                    XmlDocumentationWriter.WriteParameter(writer, mParameterName, mContent);
                    break;
                case CommentType.XmlReturns:
                    XmlDocumentationWriter.WriteReturns(writer, mContent);
                    break;
                default:
                    WriteSingleLineComments(writer);
                    break;
            }
        }

        /// <summary>
        /// 将普通注释按物理行拆分，确保每行都具有当前作用域缩进和注释前缀。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        private void WriteSingleLineComments(CodeTextWriter writer)
        {
            string normalized = mContent.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                writer.WriteLine("// " + lines[index]);
            }
        }
    }
}
