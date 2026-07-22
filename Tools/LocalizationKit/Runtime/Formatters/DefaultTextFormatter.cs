using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YokiFrame
{
    /// <summary>提供索引参数、命名参数和简单自定义标签处理。</summary>
    public sealed class DefaultTextFormatter : ITextFormatter
    {
        private const int DEFAULT_BUILDER_CAPACITY = 256;
        private readonly object mTagHandlersLock = new object();
        private readonly Dictionary<string, Func<string, string>> mTagHandlers = new Dictionary<string, Func<string, string>>();

        /// <summary>注册或替换一个标签处理器。</summary>
        public void RegisterTagHandler(string tagName, Func<string, string> handler)
        {
            if (string.IsNullOrEmpty(tagName) || handler == null)
            {
                return;
            }

            lock (mTagHandlersLock)
            {
                mTagHandlers[tagName] = handler;
            }
        }

        /// <summary>移除一个标签处理器。</summary>
        public void UnregisterTagHandler(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
            {
                return;
            }

            lock (mTagHandlersLock)
            {
                mTagHandlers.Remove(tagName);
            }
        }

        /// <inheritdoc />
        public string Format(string template, ReadOnlySpan<object> args)
        {
            if (string.IsNullOrEmpty(template) || args.Length == 0)
            {
                return template;
            }

            return FormatIndexed(template, args);
        }

        /// <inheritdoc />
        public string Format(string template, IReadOnlyDictionary<string, object> namedArgs)
        {
            if (string.IsNullOrEmpty(template) || namedArgs == null || namedArgs.Count == 0)
            {
                return template;
            }

            var builder = new StringBuilder(DEFAULT_BUILDER_CAPACITY);
            for (int index = 0; index < template.Length; index++)
            {
                if (template[index] == '{')
                {
                    if (index + 1 < template.Length && template[index + 1] == '{')
                    {
                        builder.Append('{');
                        index++;
                        continue;
                    }

                    int closeIndex = FindClosingBrace(template, index);
                    if (closeIndex >= 0)
                    {
                        string placeholder = template.Substring(index + 1, closeIndex - index - 1);
                        builder.Append(ResolveNamedPlaceholder(placeholder, namedArgs));
                        index = closeIndex;
                        continue;
                    }
                }
                else if (template[index] == '}' && index + 1 < template.Length && template[index + 1] == '}')
                {
                    builder.Append('}');
                    index++;
                    continue;
                }

                builder.Append(template[index]);
            }

            return builder.ToString();
        }

        /// <inheritdoc />
        public string ProcessTags(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var builder = new StringBuilder(DEFAULT_BUILDER_CAPACITY);
            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] != '<')
                {
                    builder.Append(text[index]);
                    continue;
                }

                int closeIndex = text.IndexOf('>', index + 1);
                if (closeIndex < 0)
                {
                    builder.Append(text[index]);
                    continue;
                }

                string tagContent = text.Substring(index + 1, closeIndex - index - 1);
                string replacement = ProcessTag(tagContent);
                builder.Append(replacement ?? text.Substring(index, closeIndex - index + 1));
                index = closeIndex;
            }

            return builder.ToString();
        }

        /// <summary>扫描模板并解析索引占位符，保留未匹配占位符原文。</summary>
        private static string FormatIndexed(string template, ReadOnlySpan<object> args)
        {
            var builder = new StringBuilder(DEFAULT_BUILDER_CAPACITY);
            for (int index = 0; index < template.Length; index++)
            {
                if (template[index] == '{')
                {
                    if (index + 1 < template.Length && template[index + 1] == '{')
                    {
                        builder.Append('{');
                        index++;
                        continue;
                    }

                    int closeIndex = FindClosingBrace(template, index);
                    if (closeIndex >= 0)
                    {
                        string placeholder = template.Substring(index + 1, closeIndex - index - 1);
                        builder.Append(ResolveIndexedPlaceholder(placeholder, args));
                        index = closeIndex;
                        continue;
                    }
                }
                else if (template[index] == '}' && index + 1 < template.Length && template[index + 1] == '}')
                {
                    builder.Append('}');
                    index++;
                    continue;
                }

                builder.Append(template[index]);
            }

            return builder.ToString();
        }

        /// <summary>查找当前占位符的结束大括号，嵌套大括号视为无效模板。</summary>
        private static int FindClosingBrace(string text, int start)
        {
            for (int index = start + 1; index < text.Length; index++)
            {
                if (text[index] == '}')
                {
                    return index;
                }

                if (text[index] == '{')
                {
                    return -1;
                }
            }

            return -1;
        }

        /// <summary>解析索引占位符并把值转换为当前文化格式。</summary>
        private static string ResolveIndexedPlaceholder(string placeholder, ReadOnlySpan<object> args)
        {
            SplitPlaceholder(placeholder, out string name, out string format);
            int index;
            if (!int.TryParse(name, out index) || index < 0 || index >= args.Length)
            {
                return "{" + placeholder + "}";
            }

            return FormatValue(args[index], format);
        }

        /// <summary>解析命名占位符，缺少参数时保留占位符。</summary>
        private static string ResolveNamedPlaceholder(string placeholder, IReadOnlyDictionary<string, object> namedArgs)
        {
            SplitPlaceholder(placeholder, out string name, out string format);
            object value;
            return namedArgs.TryGetValue(name, out value) ? FormatValue(value, format) : "{" + placeholder + "}";
        }

        /// <summary>把占位符拆成名称和可选格式说明。</summary>
        private static void SplitPlaceholder(string placeholder, out string name, out string format)
        {
            int colonIndex = placeholder.IndexOf(':');
            name = colonIndex >= 0 ? placeholder.Substring(0, colonIndex) : placeholder;
            format = colonIndex >= 0 ? placeholder.Substring(colonIndex + 1) : null;
        }

        /// <summary>把参数值转换为文本，并在存在格式说明时使用当前文化。</summary>
        private static string FormatValue(object value, string format)
        {
            if (value == null)
            {
                return string.Empty;
            }

            IFormattable formattable = value as IFormattable;
            return formattable != null && !string.IsNullOrEmpty(format)
                ? formattable.ToString(format, CultureInfo.CurrentCulture)
                : value.ToString();
        }

        /// <summary>查找标签处理器并在锁外执行用户回调。</summary>
        private string ProcessTag(string tagContent)
        {
            int colonIndex = tagContent.IndexOf(':');
            string tagName = colonIndex >= 0 ? tagContent.Substring(0, colonIndex) : tagContent;
            string argument = colonIndex >= 0 ? tagContent.Substring(colonIndex + 1) : string.Empty;
            Func<string, string> handler;
            lock (mTagHandlersLock)
            {
                if (!mTagHandlers.TryGetValue(tagName, out handler))
                {
                    return null;
                }
            }

            return handler(argument);
        }
    }
}
