using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 默认文本格式化器，支持索引参数、命名参数和简单标签处理。
    /// </summary>
    public class DefaultTextFormatter : ITextFormatter
    {
        private const int DEFAULT_BUILDER_CAPACITY = 256;

        private readonly object mTagHandlersLock = new object();
        private readonly Dictionary<string, Func<string, string>> mTagHandlers = new();

        /// <summary>
        /// 注册自定义标签处理器。
        /// </summary>
        /// <param name="tagName">标签名。</param>
        /// <param name="handler">标签参数处理函数。</param>
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

        /// <summary>
        /// 注销自定义标签处理器。
        /// </summary>
        /// <param name="tagName">标签名。</param>
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

            var builder = new StringBuilder(DEFAULT_BUILDER_CAPACITY);
            for (int i = 0; i < template.Length; i++)
            {
                char current = template[i];
                if (current == '{')
                {
                    if (i + 1 < template.Length && template[i + 1] == '{')
                    {
                        builder.Append('{');
                        i++;
                        continue;
                    }

                    int closeIndex = FindClosingBrace(template, i);
                    if (closeIndex >= 0)
                    {
                        string placeholder = template.Substring(i + 1, closeIndex - i - 1);
                        builder.Append(ResolveIndexedPlaceholder(placeholder, args));
                        i = closeIndex;
                        continue;
                    }
                }
                else if (current == '}' && i + 1 < template.Length && template[i + 1] == '}')
                {
                    builder.Append('}');
                    i++;
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        /// <inheritdoc />
        public string Format(string template, IReadOnlyDictionary<string, object> namedArgs)
        {
            if (string.IsNullOrEmpty(template) || namedArgs == null || namedArgs.Count == 0)
            {
                return template;
            }

            var builder = new StringBuilder(DEFAULT_BUILDER_CAPACITY);
            for (int i = 0; i < template.Length; i++)
            {
                char current = template[i];
                if (current == '{')
                {
                    if (i + 1 < template.Length && template[i + 1] == '{')
                    {
                        builder.Append('{');
                        i++;
                        continue;
                    }

                    int closeIndex = FindClosingBrace(template, i);
                    if (closeIndex >= 0)
                    {
                        string placeholder = template.Substring(i + 1, closeIndex - i - 1);
                        builder.Append(ResolveNamedPlaceholder(placeholder, namedArgs));
                        i = closeIndex;
                        continue;
                    }
                }
                else if (current == '}' && i + 1 < template.Length && template[i + 1] == '}')
                {
                    builder.Append('}');
                    i++;
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        /// <inheritdoc />
        public string ProcessTags(string text)
        {
            if (string.IsNullOrEmpty(text) || !HasTagHandlers())
            {
                return text;
            }

            var builder = new StringBuilder(DEFAULT_BUILDER_CAPACITY);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '<')
                {
                    builder.Append(text[i]);
                    continue;
                }

                int closeIndex = text.IndexOf('>', i + 1);
                if (closeIndex < 0)
                {
                    builder.Append(text[i]);
                    continue;
                }

                string tagContent = text.Substring(i + 1, closeIndex - i - 1);
                string processed = ProcessTag(tagContent);
                if (processed != null)
                {
                    builder.Append(processed);
                }
                else
                {
                    builder.Append(text, i, closeIndex - i + 1);
                }

                i = closeIndex;
            }

            return builder.ToString();
        }

        private static int FindClosingBrace(string text, int start)
        {
            for (int i = start + 1; i < text.Length; i++)
            {
                if (text[i] == '}')
                {
                    return i;
                }

                if (text[i] == '{')
                {
                    return -1;
                }
            }

            return -1;
        }

        private static string ResolveIndexedPlaceholder(string placeholder, ReadOnlySpan<object> args)
        {
            int colonIndex = placeholder.IndexOf(':');
            string indexText = colonIndex >= 0 ? placeholder.Substring(0, colonIndex) : placeholder;
            string format = colonIndex >= 0 ? placeholder.Substring(colonIndex + 1) : null;

            int index;
            if (!int.TryParse(indexText, out index) || index < 0 || index >= args.Length)
            {
                return "{" + placeholder + "}";
            }

            object value = args[index];
            return FormatValue(value, format);
        }

        private static string ResolveNamedPlaceholder(string placeholder, IReadOnlyDictionary<string, object> namedArgs)
        {
            int colonIndex = placeholder.IndexOf(':');
            string name = colonIndex >= 0 ? placeholder.Substring(0, colonIndex) : placeholder;
            string format = colonIndex >= 0 ? placeholder.Substring(colonIndex + 1) : null;

            object value;
            if (!namedArgs.TryGetValue(name, out value))
            {
                return "{" + placeholder + "}";
            }

            return FormatValue(value, format);
        }

        private static string FormatValue(object value, string format)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(format))
            {
                IFormattable formattable = value as IFormattable;
                if (formattable != null)
                {
                    return formattable.ToString(format, null);
                }
            }

            return value.ToString();
        }

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

        private bool HasTagHandlers()
        {
            lock (mTagHandlersLock)
            {
                return mTagHandlers.Count > 0;
            }
        }
    }
}
