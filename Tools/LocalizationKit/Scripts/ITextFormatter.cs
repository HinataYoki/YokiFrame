using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 文本格式化器接口
    /// 处理参数替换、富文本标签等
    /// </summary>
    public interface ITextFormatter
    {
        /// <summary>
        /// 格式化文本，替换索引参数占位符 {0}, {1}, {2}...
        /// </summary>
        /// <param name="template">模板文本</param>
        /// <param name="args">参数数组</param>
        /// <returns>格式化后的文本</returns>
        string Format(string template, ReadOnlySpan<object> args);

        /// <summary>
        /// 格式化文本，使用命名参数 {name}, {count}...
        /// </summary>
        /// <param name="template">模板文本</param>
        /// <param name="namedArgs">命名参数字典</param>
        /// <returns>格式化后的文本</returns>
        string Format(string template, IReadOnlyDictionary<string, object> namedArgs);

        /// <summary>
        /// 处理富文本标签
        /// </summary>
        /// <param name="text">原始文本</param>
        /// <returns>处理后的文本</returns>
        string ProcessTags(string text);
    }
}
