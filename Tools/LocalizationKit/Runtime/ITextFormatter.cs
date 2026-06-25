using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 定义本地化文本格式化与标签处理能力。
    /// </summary>
    public interface ITextFormatter
    {
        /// <summary>
        /// 使用索引参数格式化模板文本。
        /// </summary>
        /// <param name="template">模板文本。</param>
        /// <param name="args">索引参数。</param>
        /// <returns>格式化后的文本。</returns>
        string Format(string template, ReadOnlySpan<object> args);

        /// <summary>
        /// 使用命名参数格式化模板文本。
        /// </summary>
        /// <param name="template">模板文本。</param>
        /// <param name="namedArgs">命名参数。</param>
        /// <returns>格式化后的文本。</returns>
        string Format(string template, IReadOnlyDictionary<string, object> namedArgs);

        /// <summary>
        /// 处理文本中的自定义标签。
        /// </summary>
        /// <param name="text">原始文本。</param>
        /// <returns>处理后的文本。</returns>
        string ProcessTags(string text);
    }
}
