using System;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>
    /// 为大型模板提供逐行构建入口，并把每一行接入同一结构化作用域顺序。
    /// </summary>
    public sealed class CodeGenLineBuilder
    {
        private readonly ICodeContainer mScope;
        private MutableLineCode mCurrentLine;

        /// <summary>
        /// 创建逐行构建器；作用域必须由 CodeGenKit 创建。
        /// </summary>
        /// <param name="scope">接收模板行的目标作用域。</param>
        public CodeGenLineBuilder(ICodeScope scope)
        {
            mScope = CodeScopeAccess.RequireContainer(scope);
        }

        /// <summary>
        /// 向当前行追加单行文本；null 按空文本处理。
        /// </summary>
        /// <param name="value">待追加文本。</param>
        /// <returns>当前逐行构建器。</returns>
        public CodeGenLineBuilder Append(string value)
        {
            string validValue = value == null ? string.Empty : CSharpText.RequireLine(value, nameof(value));
            RequireCurrentLine().Append(validValue);
            return this;
        }

        /// <summary>
        /// 向当前行追加单个字符，CR/LF 必须改用 AppendLine 表达。
        /// </summary>
        /// <param name="value">待追加字符。</param>
        /// <returns>当前逐行构建器。</returns>
        public CodeGenLineBuilder Append(char value)
        {
            if (value == '\r' || value == '\n')
            {
                throw new ArgumentException("换行字符必须通过 AppendLine 追加。", nameof(value));
            }

            RequireCurrentLine().Append(value);
            return this;
        }

        /// <summary>
        /// 使用 InvariantCulture 向当前行追加对象文本，避免区域文化改变源码。
        /// </summary>
        /// <param name="value">待格式化对象。</param>
        /// <returns>当前逐行构建器。</returns>
        public CodeGenLineBuilder Append(object value)
        {
            return Append(CSharpText.FormatInvariant(value));
        }

        /// <summary>
        /// 使用 InvariantCulture 格式化并追加单行文本。
        /// </summary>
        /// <param name="format">复合格式字符串。</param>
        /// <param name="arguments">格式参数。</param>
        /// <returns>当前逐行构建器。</returns>
        public CodeGenLineBuilder AppendFormat(string format, params object[] arguments)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }

            return Append(string.Format(CultureInfo.InvariantCulture, format, arguments));
        }

        /// <summary>
        /// 结束当前行；当前没有内容时显式追加一个空行。
        /// </summary>
        /// <returns>当前逐行构建器。</returns>
        public CodeGenLineBuilder AppendLine()
        {
            if (mCurrentLine == null)
            {
                mScope.Add(new TextLineCode(string.Empty));
            }

            mCurrentLine = null;
            return this;
        }

        /// <summary>
        /// 追加文本并结束当前行。
        /// </summary>
        /// <param name="value">待追加的单行文本。</param>
        /// <returns>当前逐行构建器。</returns>
        public CodeGenLineBuilder AppendLine(string value)
        {
            Append(value);
            mCurrentLine = null;
            return this;
        }

        /// <summary>
        /// 结束当前行。当前实现会在首次 Append 时立即插入可变行节点，因此不调用 Flush 也不会丢失尾行。
        /// </summary>
        public void Flush()
        {
            mCurrentLine = null;
        }

        /// <summary>
        /// 获取当前可变行；首次追加时立即插入作用域以锁定与其它节点的相对顺序。
        /// </summary>
        /// <returns>当前可变行节点。</returns>
        private MutableLineCode RequireCurrentLine()
        {
            if (mCurrentLine == null)
            {
                mCurrentLine = new MutableLineCode();
                mScope.Add(mCurrentLine);
            }

            return mCurrentLine;
        }
    }
}
