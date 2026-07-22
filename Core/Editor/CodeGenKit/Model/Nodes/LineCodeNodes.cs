using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 表示已完成的单行原始源码。
    /// </summary>
    internal sealed class TextLineCode : ICodeNode
    {
        private readonly string mLine;

        /// <summary>
        /// 创建单行原始源码节点并拒绝嵌入换行符。
        /// </summary>
        /// <param name="line">单行源码；空字符串表示空行。</param>
        internal TextLineCode(string line)
        {
            mLine = CSharpText.RequireLine(line, nameof(line));
        }

        /// <summary>
        /// 按当前作用域缩进写入单行源码。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        public void Generate(CodeTextWriter writer)
        {
            writer.WriteLine(mLine);
        }
    }

    /// <summary>
    /// 表示逐行构建器已插入作用域、但仍可继续追加内容的当前行。
    /// </summary>
    internal sealed class MutableLineCode : ICodeNode
    {
        private readonly StringBuilder mBuilder = new StringBuilder(128);

        /// <summary>
        /// 追加已经过单行约束的文本。
        /// </summary>
        /// <param name="value">待追加文本。</param>
        internal void Append(string value)
        {
            mBuilder.Append(value);
        }

        /// <summary>
        /// 追加单个非换行字符。
        /// </summary>
        /// <param name="value">待追加字符。</param>
        internal void Append(char value)
        {
            mBuilder.Append(value);
        }

        /// <summary>
        /// 在最终渲染时读取当前完整行，因此尾行无需显式 Flush 也不会丢失。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        public void Generate(CodeTextWriter writer)
        {
            writer.WriteLine(mBuilder.ToString());
        }
    }

    /// <summary>
    /// 表示单个 using namespace 指令。
    /// </summary>
    internal sealed class UsingCode : ICodeNode
    {
        private readonly string mNamespaceName;

        /// <summary>
        /// 创建经过限定名称校验的 using 指令。
        /// </summary>
        /// <param name="namespaceName">要导入的命名空间。</param>
        internal UsingCode(string namespaceName)
        {
            mNamespaceName = CSharpIdentifierValidator.RequireQualifiedName(namespaceName, nameof(namespaceName));
        }

        /// <summary>
        /// 写入 using 指令并自动补充分号。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        public void Generate(CodeTextWriter writer)
        {
            writer.WriteLine("using " + mNamespaceName + ";");
        }
    }
}
