using System;
using System.Collections.Generic;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>为 Unity Runtime 提供无宿主依赖的有限 JSON 值解析。</summary>
    internal sealed class LocalizationJsonParser
    {
        private readonly string mJson;
        private int mIndex;

        /// <summary>创建绑定到单个 JSON 文本的解析器。</summary>
        private LocalizationJsonParser(string json)
        {
            mJson = json;
        }

        /// <summary>解析完整 JSON 文本，并拒绝尾随内容。</summary>
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new FormatException("JSON content is empty.");
            }

            var parser = new LocalizationJsonParser(json);
            object value = parser.ParseValue();
            parser.SkipWhitespace();
            if (parser.mIndex != parser.mJson.Length)
            {
                throw new FormatException("JSON contains trailing data.");
            }

            return value;
        }

        /// <summary>按当前字符分派对象、数组、字符串、数字或字面量解析。</summary>
        private object ParseValue()
        {
            SkipWhitespace();
            if (mIndex >= mJson.Length)
            {
                throw new FormatException("Unexpected end of JSON.");
            }

            char current = mJson[mIndex];
            if (current == '{') return ParseObject();
            if (current == '[') return ParseArray();
            if (current == '"') return ParseString();
            if (current == '-' || current >= '0' && current <= '9') return ParseNumber();
            if (MatchLiteral("true")) return true;
            if (MatchLiteral("false")) return false;
            if (MatchLiteral("null")) return null;
            throw new FormatException("Unexpected JSON token.");
        }

        /// <summary>解析 JSON 对象并以序数键保存字段。</summary>
        private Dictionary<string, object> ParseObject()
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            Expect('{');
            SkipWhitespace();
            if (TryConsume('}')) return result;

            while (true)
            {
                SkipWhitespace();
                string key = ParseString();
                SkipWhitespace();
                Expect(':');
                object value = ParseValue();
                if (result.ContainsKey(key))
                {
                    throw new FormatException("JSON object contains a duplicate key: " + key);
                }

                result.Add(key, value);
                SkipWhitespace();
                if (TryConsume('}')) return result;
                Expect(',');
            }
        }

        /// <summary>解析 JSON 数组并保持元素顺序。</summary>
        private List<object> ParseArray()
        {
            var result = new List<object>();
            Expect('[');
            SkipWhitespace();
            if (TryConsume(']')) return result;

            while (true)
            {
                result.Add(ParseValue());
                SkipWhitespace();
                if (TryConsume(']')) return result;
                Expect(',');
            }
        }

        /// <summary>解析字符串转义、Unicode 字符并拒绝控制字符。</summary>
        private string ParseString()
        {
            Expect('"');
            var chars = new List<char>();
            while (mIndex < mJson.Length)
            {
                char current = mJson[mIndex++];
                if (current == '"') return new string(chars.ToArray());
                if (current < 0x20) throw new FormatException("JSON string contains a control character.");
                if (current != '\\')
                {
                    chars.Add(current);
                    continue;
                }

                if (mIndex >= mJson.Length) throw new FormatException("Invalid JSON string escape.");
                char escape = mJson[mIndex++];
                switch (escape)
                {
                    case '"':
                    case '\\':
                    case '/': chars.Add(escape); break;
                    case 'b': chars.Add('\b'); break;
                    case 'f': chars.Add('\f'); break;
                    case 'n': chars.Add('\n'); break;
                    case 'r': chars.Add('\r'); break;
                    case 't': chars.Add('\t'); break;
                    case 'u': chars.Add(ParseUnicodeEscape()); break;
                    default: throw new FormatException("Invalid JSON string escape.");
                }
            }

            throw new FormatException("Unterminated JSON string.");
        }

        /// <summary>解析四位十六进制 Unicode 转义。</summary>
        private char ParseUnicodeEscape()
        {
            if (mIndex + 4 > mJson.Length)
            {
                throw new FormatException("Invalid unicode escape.");
            }

            string hex = mJson.Substring(mIndex, 4);
            mIndex += 4;
            return (char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        /// <summary>解析整数或浮点 JSON 数字并使用 invariant culture 转换。</summary>
        private object ParseNumber()
        {
            int start = mIndex;
            if (mJson[mIndex] == '-') mIndex++;
            while (mIndex < mJson.Length && char.IsDigit(mJson[mIndex])) mIndex++;

            bool isFloatingPoint = false;
            if (mIndex < mJson.Length && mJson[mIndex] == '.')
            {
                isFloatingPoint = true;
                mIndex++;
                while (mIndex < mJson.Length && char.IsDigit(mJson[mIndex])) mIndex++;
            }

            if (mIndex < mJson.Length && (mJson[mIndex] == 'e' || mJson[mIndex] == 'E'))
            {
                isFloatingPoint = true;
                mIndex++;
                if (mIndex < mJson.Length && (mJson[mIndex] == '+' || mJson[mIndex] == '-')) mIndex++;
                while (mIndex < mJson.Length && char.IsDigit(mJson[mIndex])) mIndex++;
            }

            string text = mJson.Substring(start, mIndex - start);
            return isFloatingPoint
                ? (object)double.Parse(text, CultureInfo.InvariantCulture)
                : long.Parse(text, CultureInfo.InvariantCulture);
        }

        /// <summary>尝试消费 true、false 或 null 等 JSON 字面量。</summary>
        private bool MatchLiteral(string literal)
        {
            if (mIndex + literal.Length > mJson.Length) return false;
            for (int offset = 0; offset < literal.Length; offset++)
            {
                if (mJson[mIndex + offset] != literal[offset]) return false;
            }

            mIndex += literal.Length;
            return true;
        }

        /// <summary>尝试消费一个结构标记字符。</summary>
        private bool TryConsume(char expected)
        {
            if (mIndex < mJson.Length && mJson[mIndex] == expected)
            {
                mIndex++;
                return true;
            }

            return false;
        }

        /// <summary>消费必需结构标记，不匹配时抛出格式异常。</summary>
        private void Expect(char expected)
        {
            if (mIndex >= mJson.Length || mJson[mIndex] != expected)
            {
                throw new FormatException("Expected '" + expected + "'.");
            }

            mIndex++;
        }

        /// <summary>跳过 JSON 允许的四类空白字符。</summary>
        private void SkipWhitespace()
        {
            while (mIndex < mJson.Length)
            {
                char current = mJson[mIndex];
                if (current != ' ' && current != '\t' && current != '\r' && current != '\n') return;
                mIndex++;
            }
        }
    }
}
