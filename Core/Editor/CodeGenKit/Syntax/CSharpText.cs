using System;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>
    /// 集中处理原始 C# 片段的单行约束、XML 转义和区域无关格式化。
    /// </summary>
    internal static class CSharpText
    {
        /// <summary>
        /// 验证必填文本非空且不包含物理换行，防止破坏作用域缩进。
        /// </summary>
        /// <param name="value">待验证文本。</param>
        /// <param name="parameterName">异常中使用的参数名。</param>
        /// <returns>原始文本，便于构造函数直接赋值。</returns>
        internal static string RequireNonEmptyLine(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("C# 代码片段不能为空。", parameterName);
            }

            return RequireLine(value, parameterName);
        }

        /// <summary>
        /// 验证文本不包含 CR/LF；空字符串可用于显式空行，null 会被拒绝。
        /// </summary>
        /// <param name="value">待验证文本。</param>
        /// <param name="parameterName">异常中使用的参数名。</param>
        /// <returns>原始单行文本。</returns>
        internal static string RequireLine(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
            {
                throw new ArgumentException("单行代码片段不能包含换行符。", parameterName);
            }

            return value;
        }

        /// <summary>
        /// 将对象转换为区域无关文本，确保数值字面量不受系统文化影响。
        /// </summary>
        /// <param name="value">待格式化对象；null 生成空文本。</param>
        /// <returns>使用 InvariantCulture 的文本。</returns>
        internal static string FormatInvariant(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }

        /// <summary>
        /// 转义 XML 文本节点中的保留字符，供 summary、param 和 returns 共用。
        /// </summary>
        /// <param name="value">原始文档文本。</param>
        /// <returns>可安全写入 XML 文档节点的文本。</returns>
        internal static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
