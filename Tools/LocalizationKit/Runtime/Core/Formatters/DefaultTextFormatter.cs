using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 默认文本格式化器
    /// 支持索引占位符 {0}, {1}，命名占位符 {name}，格式说明符 {0:F2}
    /// 使用 StringBuilder 优化字符串拼接
    /// </summary>
    public class DefaultTextFormatter : ITextFormatter
    {
        // 缓存 StringBuilder 避免频繁分配
        private readonly StringBuilder mBuilder = new(256);
        
        // 自定义标签处理器
        private readonly Dictionary<string, Func<string, string>> mTagHandlers = new();

        /// <summary>
        /// 注册自定义标签处理器
        /// </summary>
        /// <param name="tagName">标签名（不含尖括号）</param>
        /// <param name="handler">处理函数，参数为标签内容，返回替换后的文本</param>
        public void RegisterTagHandler(string tagName, Func<string, string> handler)
        {
            if (string.IsNullOrEmpty(tagName) || handler == null) return;
            mTagHandlers[tagName] = handler;
        }

        /// <summary>
        /// 移除自定义标签处理器
        /// </summary>
        public void UnregisterTagHandler(string tagName)
        {
            if (string.IsNullOrEmpty(tagName)) return;
            mTagHandlers.Remove(tagName);
        }

        public string Format(string template, ReadOnlySpan<object> args)
        {
            if (string.IsNullOrEmpty(template)) return template;
            if (args.Length == 0) return template;

            mBuilder.Clear();
            var templateSpan = template.AsSpan();
            int i = 0;

            while (i < templateSpan.Length)
            {
                if (templateSpan[i] == '{')
                {
                    // 检查是否是转义 {{
                    if (i + 1 < templateSpan.Length && templateSpan[i + 1] == '{')
                    {
                        mBuilder.Append('{');
                        i += 2;
                        continue;
                    }

                    // 查找闭合括号
                    int closeIndex = FindClosingBrace(templateSpan, i);
                    if (closeIndex == -1)
                    {
                        mBuilder.Append(templateSpan[i]);
                        i++;
                        continue;
                    }

                    // 解析占位符内容
                    var placeholder = templateSpan.Slice(i + 1, closeIndex - i - 1);
                    var replacement = ResolveIndexedPlaceholder(placeholder, args);
                    mBuilder.Append(replacement);
                    i = closeIndex + 1;
                }
                else if (templateSpan[i] == '}')
                {
                    // 检查是否是转义 }}
                    if (i + 1 < templateSpan.Length && templateSpan[i + 1] == '}')
                    {
                        mBuilder.Append('}');
                        i += 2;
                        continue;
                    }
                    mBuilder.Append(templateSpan[i]);
                    i++;
                }
                else
                {
                    mBuilder.Append(templateSpan[i]);
                    i++;
                }
            }

            return mBuilder.ToString();
        }

        public string Format(string template, IReadOnlyDictionary<string, object> namedArgs)
        {
            if (string.IsNullOrEmpty(template)) return template;
            if (namedArgs == null || namedArgs.Count == 0) return template;

            mBuilder.Clear();
            var templateSpan = template.AsSpan();
            int i = 0;

            while (i < templateSpan.Length)
            {
                if (templateSpan[i] == '{')
                {
                    // 检查是否是转义 {{
                    if (i + 1 < templateSpan.Length && templateSpan[i + 1] == '{')
                    {
                        mBuilder.Append('{');
                        i += 2;
                        continue;
                    }

                    // 查找闭合括号
                    int closeIndex = FindClosingBrace(templateSpan, i);
                    if (closeIndex == -1)
                    {
                        mBuilder.Append(templateSpan[i]);
                        i++;
                        continue;
                    }

                    // 解析占位符内容
                    var placeholder = templateSpan.Slice(i + 1, closeIndex - i - 1);
                    var replacement = ResolveNamedPlaceholder(placeholder, namedArgs);
                    mBuilder.Append(replacement);
                    i = closeIndex + 1;
                }
                else if (templateSpan[i] == '}')
                {
                    // 检查是否是转义 }}
                    if (i + 1 < templateSpan.Length && templateSpan[i + 1] == '}')
                    {
                        mBuilder.Append('}');
                        i += 2;
                        continue;
                    }
                    mBuilder.Append(templateSpan[i]);
                    i++;
                }
                else
                {
                    mBuilder.Append(templateSpan[i]);
                    i++;
                }
            }

            return mBuilder.ToString();
        }

        public string ProcessTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (mTagHandlers.Count == 0) return text;

            mBuilder.Clear();
            var textSpan = text.AsSpan();
            int i = 0;

            while (i < textSpan.Length)
            {
                if (textSpan[i] == '<')
                {
                    // 查找标签结束
                    int closeIndex = FindTagClose(textSpan, i);
                    if (closeIndex == -1)
                    {
                        mBuilder.Append(textSpan[i]);
                        i++;
                        continue;
                    }

                    var tagContent = textSpan.Slice(i + 1, closeIndex - i - 1);
                    var processed = ProcessTag(tagContent);
                    
                    if (processed != null)
                    {
                        mBuilder.Append(processed);
                    }
                    else
                    {
                        // 保留原始标签（Unity 富文本标签）
                        mBuilder.Append(textSpan.Slice(i, closeIndex - i + 1));
                    }
                    i = closeIndex + 1;
                }
                else
                {
                    mBuilder.Append(textSpan[i]);
                    i++;
                }
            }

            return mBuilder.ToString();
        }

        private static int FindClosingBrace(ReadOnlySpan<char> span, int start)
        {
            for (int i = start + 1; i < span.Length; i++)
            {
                if (span[i] == '}') return i;
                if (span[i] == '{') return -1; // 嵌套不支持
            }
            return -1;
        }

        private static int FindTagClose(ReadOnlySpan<char> span, int start)
        {
            for (int i = start + 1; i < span.Length; i++)
            {
                if (span[i] == '>') return i;
            }
            return -1;
        }

        private string ResolveIndexedPlaceholder(ReadOnlySpan<char> placeholder, ReadOnlySpan<object> args)
        {
            // 解析格式：index 或 index:format
            int colonIndex = placeholder.IndexOf(':');
            ReadOnlySpan<char> indexPart;
            ReadOnlySpan<char> formatPart = default;

            if (colonIndex >= 0)
            {
                indexPart = placeholder.Slice(0, colonIndex);
                formatPart = placeholder.Slice(colonIndex + 1);
            }
            else
            {
                indexPart = placeholder;
            }

            // 解析索引
            if (!int.TryParse(indexPart, out int index) || index < 0 || index >= args.Length)
            {
                // 保留原始占位符用于调试
                return $"{{{placeholder.ToString()}}}";
            }

            var arg = args[index];
            if (arg == null) return string.Empty;

            // 应用格式说明符
            if (formatPart.Length > 0 && arg is IFormattable formattable)
            {
                return formattable.ToString(formatPart.ToString(), null);
            }

            return arg.ToString();
        }

        private string ResolveNamedPlaceholder(ReadOnlySpan<char> placeholder, IReadOnlyDictionary<string, object> namedArgs)
        {
            // 解析格式：name 或 name:format
            int colonIndex = placeholder.IndexOf(':');
            string name;
            string format = null;

            if (colonIndex >= 0)
            {
                name = placeholder.Slice(0, colonIndex).ToString();
                format = placeholder.Slice(colonIndex + 1).ToString();
            }
            else
            {
                name = placeholder.ToString();
            }

            // 查找参数
            if (!namedArgs.TryGetValue(name, out var arg))
            {
                // 保留原始占位符用于调试
                return $"{{{placeholder.ToString()}}}";
            }

            if (arg == null) return string.Empty;

            // 应用格式说明符
            if (!string.IsNullOrEmpty(format) && arg is IFormattable formattable)
            {
                return formattable.ToString(format, null);
            }

            return arg.ToString();
        }

        private string ProcessTag(ReadOnlySpan<char> tagContent)
        {
            // 解析标签名和参数：tagName:param 或 tagName
            int colonIndex = tagContent.IndexOf(':');
            string tagName;
            string param;

            if (colonIndex >= 0)
            {
                tagName = tagContent.Slice(0, colonIndex).ToString();
                param = tagContent.Slice(colonIndex + 1).ToString();
            }
            else
            {
                tagName = tagContent.ToString();
                param = string.Empty;
            }

            // 查找处理器
            if (mTagHandlers.TryGetValue(tagName, out var handler))
            {
                return handler(param);
            }

            // 返回 null 表示保留原始标签
            return null;
        }
    }
}
