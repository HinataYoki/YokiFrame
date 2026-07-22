namespace YokiFrame
{
    /// <summary>
    /// 统一生成已转义的 C# XML 文档节点，避免各成员重复拼接标签。
    /// </summary>
    internal static class XmlDocumentationWriter
    {
        /// <summary>
        /// 写入可跨多行的 summary；空内容不生成文档节点。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        /// <param name="summary">原始 summary 文本。</param>
        internal static void WriteSummary(CodeTextWriter writer, string summary)
        {
            if (string.IsNullOrEmpty(summary))
            {
                return;
            }

            writer.WriteLine("/// <summary>");
            WriteTextLines(writer, summary);
            writer.WriteLine("/// </summary>");
        }

        /// <summary>
        /// 写入参数文档并转义参数名与说明中的 XML 保留字符。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        /// <param name="parameterName">已验证的 C# 参数名。</param>
        /// <param name="description">参数说明。</param>
        internal static void WriteParameter(CodeTextWriter writer, string parameterName, string description)
        {
            if (string.IsNullOrEmpty(description))
            {
                return;
            }

            writer.WriteLine("/// <param name=\"" + CSharpText.EscapeXml(parameterName) + "\">"
                + EscapeInline(description) + "</param>");
        }

        /// <summary>
        /// 写入返回值文档并把换行转换为 XML 字符引用。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        /// <param name="description">返回值说明。</param>
        internal static void WriteReturns(CodeTextWriter writer, string description)
        {
            if (!string.IsNullOrEmpty(description))
            {
                writer.WriteLine("/// <returns>" + EscapeInline(description) + "</returns>");
            }
        }

        /// <summary>
        /// 将 summary 按物理行拆分，每行独立转义并保持文档注释前缀。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        /// <param name="value">原始多行文本。</param>
        private static void WriteTextLines(CodeTextWriter writer, string value)
        {
            string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                writer.WriteLine("/// " + CSharpText.EscapeXml(lines[index]));
            }
        }

        /// <summary>
        /// 转义单行 XML 内容，并把原始换行表达为字符引用以保持标签完整。
        /// </summary>
        /// <param name="value">原始文档文本。</param>
        /// <returns>可嵌入单行 XML 标签的内容。</returns>
        private static string EscapeInline(string value)
        {
            return CSharpText.EscapeXml(value)
                .Replace("\r\n", "&#10;")
                .Replace("\r", "&#10;")
                .Replace("\n", "&#10;");
        }
    }
}
