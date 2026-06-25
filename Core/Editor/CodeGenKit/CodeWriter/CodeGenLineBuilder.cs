using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 面向模板迁移的逐行构建器，统一把生成内容写入 CodeGenKit 作用域。
    /// </summary>
    public sealed class CodeGenLineBuilder
    {
        private readonly ICodeScope scope;
        private readonly StringBuilder lineBuilder = new StringBuilder(128);

        public CodeGenLineBuilder(ICodeScope scope)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            this.scope = scope;
        }

        public CodeGenLineBuilder Append(string value)
        {
            lineBuilder.Append(value);
            return this;
        }

        public CodeGenLineBuilder Append(char value)
        {
            lineBuilder.Append(value);
            return this;
        }

        public CodeGenLineBuilder Append(object value)
        {
            lineBuilder.Append(value);
            return this;
        }

        public CodeGenLineBuilder AppendFormat(string format, params object[] args)
        {
            lineBuilder.AppendFormat(format, args);
            return this;
        }

        public CodeGenLineBuilder AppendLine()
        {
            FlushLine();
            return this;
        }

        public CodeGenLineBuilder AppendLine(string value)
        {
            lineBuilder.Append(value);
            FlushLine();
            return this;
        }

        public void Flush()
        {
            if (lineBuilder.Length > 0)
            {
                FlushLine();
            }
        }

        private void FlushLine()
        {
            scope.Custom(lineBuilder.ToString());
            lineBuilder.Length = 0;
        }
    }
}
