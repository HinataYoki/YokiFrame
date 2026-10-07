#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame.Json
{
    /// <summary>
    /// 最小递归下降 JSON 解析器；任何语法问题都抛出 <see cref="JsonException"/>。
    /// </summary>
    internal sealed class JsonReader
    {
        private readonly string mText;
        private readonly int mMaxDepth;
        private int mIndex;

        internal JsonReader(string text, int maxDepth)
        {
            mText = text;
            mMaxDepth = maxDepth;
        }

        /// <summary>
        /// 读取完整文档并要求没有多余内容。
        /// </summary>
        /// <returns>根节点。</returns>
        internal JsonNode ReadDocument()
        {
            SkipWhitespace();
            if (mIndex >= mText.Length)
            {
                throw new JsonException("JSON document is empty.");
            }

            JsonNode root = ReadValue(0);
            SkipWhitespace();
            if (mIndex != mText.Length)
            {
                throw new JsonException("JSON document contains trailing content at position " + mIndex + ".");
            }

            return root;
        }

        /// <summary>
        /// 读取任意值。
        /// </summary>
        /// <param name="depth">当前嵌套深度。</param>
        /// <returns>节点。</returns>
        private JsonNode ReadValue(int depth)
        {
            if (depth > mMaxDepth)
            {
                throw new JsonException("JSON document exceeds the maximum depth of " + mMaxDepth + ".");
            }

            SkipWhitespace();
            if (mIndex >= mText.Length)
            {
                throw new JsonException("JSON document ended unexpectedly.");
            }

            switch (mText[mIndex])
            {
                case '{':
                    return ReadObject(depth);
                case '[':
                    return ReadArray(depth);
                case '"':
                    return ReadStringNode();
                case 't':
                    return ReadLiteral("true", JsonValueKind.True);
                case 'f':
                    return ReadLiteral("false", JsonValueKind.False);
                case 'n':
                    return ReadLiteral("null", JsonValueKind.Null);
                default:
                    return ReadNumberNode();
            }
        }

        /// <summary>
        /// 读取对象。
        /// </summary>
        /// <param name="depth">当前嵌套深度。</param>
        /// <returns>对象节点。</returns>
        private JsonNode ReadObject(int depth)
        {
            int start = mIndex;
            mIndex++;
            var node = new JsonNode { Kind = JsonValueKind.Object, Members = new List<JsonMember>() };
            SkipWhitespace();
            if (mIndex < mText.Length && mText[mIndex] == '}')
            {
                mIndex++;
                node.Start = start;
                node.End = mIndex;
                return node;
            }

            while (true)
            {
                SkipWhitespace();
                if (mIndex >= mText.Length || mText[mIndex] != '"')
                {
                    throw new JsonException("JSON object property name is missing at position " + mIndex + ".");
                }

                string name = ReadString();
                SkipWhitespace();
                if (mIndex >= mText.Length || mText[mIndex] != ':')
                {
                    throw new JsonException("JSON object property '" + name + "' is missing a value at position " + mIndex + ".");
                }

                mIndex++;
                JsonNode value = ReadValue(depth + 1);
                for (var index = 0; index < node.Members.Count; index++)
                {
                    if (string.Equals(node.Members[index].Name, name, StringComparison.Ordinal))
                    {
                        throw new JsonException("JSON object contains duplicate property '" + name + "'.");
                    }
                }

                node.Members.Add(new JsonMember { Name = name, Value = value });
                SkipWhitespace();
                if (mIndex >= mText.Length)
                {
                    throw new JsonException("JSON object is not terminated.");
                }

                if (mText[mIndex] == ',')
                {
                    mIndex++;
                    continue;
                }

                if (mText[mIndex] == '}')
                {
                    mIndex++;
                    node.Start = start;
                    node.End = mIndex;
                    return node;
                }

                throw new JsonException("JSON object contains an unexpected character at position " + mIndex + ".");
            }
        }

        /// <summary>
        /// 读取数组。
        /// </summary>
        /// <param name="depth">当前嵌套深度。</param>
        /// <returns>数组节点。</returns>
        private JsonNode ReadArray(int depth)
        {
            int start = mIndex;
            mIndex++;
            var node = new JsonNode { Kind = JsonValueKind.Array, Items = new List<JsonNode>() };
            SkipWhitespace();
            if (mIndex < mText.Length && mText[mIndex] == ']')
            {
                mIndex++;
                node.Start = start;
                node.End = mIndex;
                return node;
            }

            while (true)
            {
                node.Items.Add(ReadValue(depth + 1));
                SkipWhitespace();
                if (mIndex >= mText.Length)
                {
                    throw new JsonException("JSON array is not terminated.");
                }

                if (mText[mIndex] == ',')
                {
                    mIndex++;
                    continue;
                }

                if (mText[mIndex] == ']')
                {
                    mIndex++;
                    node.Start = start;
                    node.End = mIndex;
                    return node;
                }

                throw new JsonException("JSON array contains an unexpected character at position " + mIndex + ".");
            }
        }

        /// <summary>
        /// 读取字符串节点。
        /// </summary>
        /// <returns>字符串节点。</returns>
        private JsonNode ReadStringNode()
        {
            int start = mIndex;
            string value = ReadString();
            return new JsonNode { Kind = JsonValueKind.String, Text = value, Start = start, End = mIndex };
        }

        /// <summary>
        /// 读取并解码字符串字面量。
        /// </summary>
        /// <returns>解码后的字符串。</returns>
        private string ReadString()
        {
            mIndex++;
            var builder = new StringBuilder();
            while (mIndex < mText.Length)
            {
                char c = mText[mIndex];
                if (c == '"')
                {
                    mIndex++;
                    return builder.ToString();
                }

                if (c != '\\')
                {
                    AppendLiteral(builder, c);
                    continue;
                }

                AppendEscape(builder);
            }

            throw new JsonException("JSON string is not terminated.");
        }

        /// <summary>追加未转义字符，并拒绝控制字符。</summary>
        /// <param name="builder">结果缓冲。</param>
        /// <param name="value">当前字符。</param>
        private void AppendLiteral(StringBuilder builder, char value)
        {
            if (value < ' ')
                throw new JsonException("JSON string contains an unescaped control character at position " + mIndex + ".");
            builder.Append(value);
            mIndex++;
        }

        /// <summary>解码一个转义序列。</summary>
        /// <param name="builder">结果缓冲。</param>
        private void AppendEscape(StringBuilder builder)
        {
            mIndex++;
            if (mIndex >= mText.Length) throw new JsonException("JSON string escape is incomplete.");
            char escape = mText[mIndex];
            mIndex++;
            switch (escape)
            {
                case '"': builder.Append('"'); break;
                case '\\': builder.Append('\\'); break;
                case '/': builder.Append('/'); break;
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'u': builder.Append(ReadUnicodeEscape()); break;
                default:
                    throw new JsonException("JSON string contains an invalid escape at position " + (mIndex - 1) + ".");
            }
        }

        /// <summary>
        /// 读取 unicode 转义并处理代理对。
        /// </summary>
        /// <returns>解码后的字符串。</returns>
        private string ReadUnicodeEscape()
        {
            int code = ReadHex4();
            if (code >= 0xD800 && code <= 0xDBFF
                && mIndex + 1 < mText.Length
                && mText[mIndex] == '\\'
                && mText[mIndex + 1] == 'u')
            {
                int saved = mIndex;
                mIndex += 2;
                int low = ReadHex4();
                if (low >= 0xDC00 && low <= 0xDFFF)
                {
                    return char.ConvertFromUtf32(0x10000 + ((code - 0xD800) << 10) + (low - 0xDC00));
                }

                mIndex = saved;
            }

            return ((char)code).ToString();
        }

        /// <summary>
        /// 读取 4 位十六进制。
        /// </summary>
        /// <returns>码位。</returns>
        private int ReadHex4()
        {
            if (mIndex + 4 > mText.Length)
            {
                throw new JsonException("JSON unicode escape is incomplete.");
            }

            int value = 0;
            for (var index = 0; index < 4; index++)
            {
                char c = mText[mIndex + index];
                int digit;
                if (c >= '0' && c <= '9')
                {
                    digit = c - '0';
                }
                else if (c >= 'a' && c <= 'f')
                {
                    digit = c - 'a' + 10;
                }
                else if (c >= 'A' && c <= 'F')
                {
                    digit = c - 'A' + 10;
                }
                else
                {
                    throw new JsonException("JSON unicode escape contains a non-hex character at position " + (mIndex + index) + ".");
                }

                value = (value << 4) + digit;
            }

            mIndex += 4;
            return value;
        }

        /// <summary>
        /// 读取 true/false/null。
        /// </summary>
        /// <param name="literal">期望字面量。</param>
        /// <param name="kind">对应值类型。</param>
        /// <returns>字面量节点。</returns>
        private JsonNode ReadLiteral(string literal, JsonValueKind kind)
        {
            int start = mIndex;
            if (mIndex + literal.Length > mText.Length
                || string.CompareOrdinal(mText, mIndex, literal, 0, literal.Length) != 0)
            {
                throw new JsonException("JSON literal is invalid at position " + mIndex + ".");
            }

            mIndex += literal.Length;
            return new JsonNode { Kind = kind, Text = literal, Start = start, End = mIndex };
        }

        /// <summary>
        /// 读取数字。
        /// </summary>
        /// <returns>数字节点。</returns>
        private JsonNode ReadNumberNode()
        {
            int start = mIndex;
            if (mIndex < mText.Length && mText[mIndex] == '-') mIndex++;
            ReadIntegerDigits(start);
            ReadFraction(start);
            ReadExponent(start);
            if (mIndex < mText.Length && !IsValueTerminator(mText[mIndex]))
                throw new JsonException("JSON number is followed by an invalid character at position " + mIndex + ".");
            return new JsonNode
            {
                Kind = JsonValueKind.Number,
                Text = mText.Substring(start, mIndex - start),
                Start = start,
                End = mIndex
            };
        }

        /// <summary>读取整数部分。前导零只允许单独一个 0。</summary>
        /// <param name="start">数字起始位置，用于错误定位。</param>
        private void ReadIntegerDigits(int start)
        {
            int integerStart = mIndex;
            if (mIndex < mText.Length && mText[mIndex] == '0') mIndex++;
            else while (mIndex < mText.Length && IsDigit(mText[mIndex])) mIndex++;
            if (mIndex == integerStart)
                throw new JsonException("JSON number is invalid at position " + start + ".");
        }

        /// <summary>读取可选小数部分。</summary>
        /// <param name="start">数字起始位置。</param>
        private void ReadFraction(int start)
        {
            if (mIndex >= mText.Length || mText[mIndex] != '.') return;
            mIndex++;
            int fractionStart = mIndex;
            while (mIndex < mText.Length && IsDigit(mText[mIndex])) mIndex++;
            if (mIndex == fractionStart)
                throw new JsonException("JSON number fraction is invalid at position " + start + ".");
        }

        /// <summary>读取可选指数部分。</summary>
        /// <param name="start">数字起始位置。</param>
        private void ReadExponent(int start)
        {
            if (mIndex >= mText.Length || (mText[mIndex] != 'e' && mText[mIndex] != 'E')) return;
            mIndex++;
            if (mIndex < mText.Length && (mText[mIndex] == '+' || mText[mIndex] == '-')) mIndex++;
            int exponentStart = mIndex;
            while (mIndex < mText.Length && IsDigit(mText[mIndex])) mIndex++;
            if (mIndex == exponentStart)
                throw new JsonException("JSON number exponent is invalid at position " + start + ".");
        }

        /// <summary>判断字符是否为十进制数字。</summary>
        /// <param name="value">待判断字符。</param>
        /// <returns>是数字时返回 true。</returns>
        private static bool IsDigit(char value)
        {
            return value >= '0' && value <= '9';
        }

        /// <summary>
        /// 判断字符能否结束一个值。
        /// </summary>
        /// <param name="value">待判断字符。</param>
        /// <returns>是分隔符时返回 true。</returns>
        private static bool IsValueTerminator(char value)
        {
            return value == ' ' || value == '\t' || value == '\r' || value == '\n'
                || value == ',' || value == '}' || value == ']';
        }

        /// <summary>
        /// 跳过空白字符。
        /// </summary>
        private void SkipWhitespace()
        {
            while (mIndex < mText.Length)
            {
                char c = mText[mIndex];
                if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
                {
                    return;
                }

                mIndex++;
            }
        }
    }
}
#endif
