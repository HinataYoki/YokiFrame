#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 校验命令 payload 使用的完整 JSON 语法，不解释协议字段或业务语义。
    /// </summary>
    internal static class YokiFrameJsonSyntaxValidator
    {
        /// <summary>
        /// 允许的最大嵌套层数。
        /// 取值 64 与 <c>System.Text.Json</c> 的默认 <c>MaxDepth</c> 一致，也与工具侧
        /// <c>CommandEnvelope</c> 和 Runtime 缓存校验使用的上限一致；协议在两端本就不可能
        /// 接受更深的 payload，因此该上限不缩小可用能力。
        /// 必要性：本类用递归下降解析，深层嵌套会耗尽线程栈并触发<b>不可捕获</b>的栈溢出，
        /// 直接终止宿主进程，故必须在进入容器前显式限深。
        /// </summary>
        private const int MAX_DEPTH = 64;

        /// <summary>
        /// 验证文本是完整 JSON 值；空 payload 按空对象处理以兼容旧命令。
        /// </summary>
        /// <param name="json">待验证 JSON 文本。</param>
        /// <exception cref="FormatException">JSON 语法不完整或包含非法 token 时抛出。</exception>
        public static void EnsureValidJson(string json)
        {
            var text = string.IsNullOrWhiteSpace(json) ? "{}" : json;
            var index = 0;
            SkipWhitespace(text, ref index);
            ParseValue(text, ref index, 0);
            SkipWhitespace(text, ref index);
            if (index != text.Length)
            {
                throw new FormatException("JSON contains trailing characters.");
            }
        }

        /// <summary>解析一个 JSON 值并推进索引。</summary>
        /// <param name="json">完整 JSON 文本。</param>
        /// <param name="index">当前解析位置。</param>
        /// <param name="depth">当前值所处的嵌套层数，顶层为 0；进入容器前先校验上限。</param>
        private static void ParseValue(string json, ref int index, int depth)
        {
            if (depth >= MAX_DEPTH)
            {
                // 递归下降会在进入下一层容器时占用栈帧；在此拦截可避免不可捕获的栈溢出。
                throw new FormatException("JSON nesting is too deep.");
            }

            if (index >= json.Length)
            {
                throw new FormatException("JSON value is missing.");
            }

            switch (json[index])
            {
                case '{': ParseObject(json, ref index, depth); return;
                case '[': ParseArray(json, ref index, depth); return;
                case '"': ParseString(json, ref index); return;
                case 't': ParseLiteral(json, ref index, "true"); return;
                case 'f': ParseLiteral(json, ref index, "false"); return;
                case 'n': ParseLiteral(json, ref index, "null"); return;
                default:
                    if (json[index] == '-' || (json[index] >= '0' && json[index] <= '9'))
                    {
                        ParseNumber(json, ref index);
                        return;
                    }

                    throw new FormatException("JSON value token is invalid.");
            }
        }

        /// <summary>解析 JSON 对象。</summary>
        /// <param name="json">完整 JSON 文本。</param>
        /// <param name="index">当前解析位置。</param>
        /// <param name="depth">本对象所处层数；其成员值在 depth + 1 层解析。</param>
        private static void ParseObject(string json, ref int index, int depth)
        {
            index++;
            SkipWhitespace(json, ref index);
            if (Consume(json, ref index, '}'))
            {
                return;
            }

            while (true)
            {
                if (index >= json.Length || json[index] != '"')
                {
                    throw new FormatException("JSON object property name is missing.");
                }

                ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                if (!Consume(json, ref index, ':'))
                {
                    throw new FormatException("JSON object property separator is missing.");
                }

                ParseValue(json, ref index, depth + 1);
                SkipWhitespace(json, ref index);
                if (Consume(json, ref index, '}'))
                {
                    return;
                }

                if (!Consume(json, ref index, ','))
                {
                    throw new FormatException("JSON object comma is missing.");
                }

                SkipWhitespace(json, ref index);
            }
        }

        /// <summary>解析 JSON 数组。</summary>
        /// <param name="json">完整 JSON 文本。</param>
        /// <param name="index">当前解析位置。</param>
        /// <param name="depth">本数组所处层数；其元素在 depth + 1 层解析。</param>
        private static void ParseArray(string json, ref int index, int depth)
        {
            index++;
            SkipWhitespace(json, ref index);
            if (Consume(json, ref index, ']'))
            {
                return;
            }

            while (true)
            {
                ParseValue(json, ref index, depth + 1);
                SkipWhitespace(json, ref index);
                if (Consume(json, ref index, ']'))
                {
                    return;
                }

                if (!Consume(json, ref index, ','))
                {
                    throw new FormatException("JSON array comma is missing.");
                }

                SkipWhitespace(json, ref index);
            }
        }

        /// <summary>解析 JSON 字符串及标准转义。</summary>
        private static void ParseString(string json, ref int index)
        {
            index++;
            while (index < json.Length)
            {
                var value = json[index++];
                if (value == '"')
                {
                    return;
                }

                if (value < 0x20)
                {
                    throw new FormatException("JSON string contains an unescaped control character.");
                }

                if (value != (char)92)
                {
                    continue;
                }

                if (index >= json.Length)
                {
                    throw new FormatException("JSON string escape is incomplete.");
                }

                var escape = json[index++];
                if (escape == 'u')
                {
                    if (index + 4 > json.Length)
                    {
                        throw new FormatException("JSON Unicode escape is incomplete.");
                    }

                    for (var offset = 0; offset < 4; offset++)
                    {
                        if (!IsHexDigit(json[index + offset]))
                        {
                            throw new FormatException("JSON Unicode escape is invalid.");
                        }
                    }

                    index += 4;
                }
                else if (escape != '"' && escape != (char)92 && escape != '/'
                    && escape != 'b' && escape != 'f' && escape != 'n'
                    && escape != 'r' && escape != 't')
                {
                    throw new FormatException("JSON string escape is invalid.");
                }
            }

            throw new FormatException("JSON string is unterminated.");
        }

        /// <summary>解析 JSON 数字。</summary>
        private static void ParseNumber(string json, ref int index)
        {
            Consume(json, ref index, '-');
            if (Consume(json, ref index, '0'))
            {
                if (index < json.Length && json[index] >= '0' && json[index] <= '9')
                {
                    throw new FormatException("JSON number contains a leading zero.");
                }
            }
            else
            {
                RequireDigit(json, ref index);
                while (index < json.Length && json[index] >= '0' && json[index] <= '9')
                {
                    index++;
                }
            }

            if (Consume(json, ref index, '.'))
            {
                RequireDigit(json, ref index);
                while (index < json.Length && json[index] >= '0' && json[index] <= '9')
                {
                    index++;
                }
            }

            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                index++;
                Consume(json, ref index, '+');
                Consume(json, ref index, '-');
                RequireDigit(json, ref index);
                while (index < json.Length && json[index] >= '0' && json[index] <= '9')
                {
                    index++;
                }
            }
        }

        /// <summary>解析 JSON 字面量。</summary>
        private static void ParseLiteral(string json, ref int index, string literal)
        {
            if (index + literal.Length > json.Length
                || !string.Equals(json.Substring(index, literal.Length), literal, StringComparison.Ordinal))
            {
                throw new FormatException("JSON literal is invalid.");
            }

            index += literal.Length;
        }

        /// <summary>要求当前位置为数字并推进。</summary>
        private static void RequireDigit(string json, ref int index)
        {
            if (index >= json.Length || json[index] < '0' || json[index] > '9')
            {
                throw new FormatException("JSON number digit is missing.");
            }

            index++;
        }

        /// <summary>消费指定字符。</summary>
        private static bool Consume(string json, ref int index, char expected)
        {
            if (index < json.Length && json[index] == expected)
            {
                index++;
                return true;
            }

            return false;
        }

        /// <summary>判断字符是否为十六进制数字。</summary>
        private static bool IsHexDigit(char value)
        {
            return (value >= '0' && value <= '9')
                || (value >= 'a' && value <= 'f')
                || (value >= 'A' && value <= 'F');
        }

        /// <summary>跳过 JSON 允许的空白字符。</summary>
        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }
        }
    }
}
#endif
