using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>定义本地化模板的参数格式化和标签处理能力。</summary>
    public interface ITextFormatter
    {
        /// <summary>按索引参数格式化模板。</summary>
        string Format(string template, ReadOnlySpan<object> args);
        /// <summary>按命名参数格式化模板。</summary>
        string Format(string template, IReadOnlyDictionary<string, object> namedArgs);
        /// <summary>执行已注册的自定义标签处理器。</summary>
        string ProcessTags(string text);
    }
}
