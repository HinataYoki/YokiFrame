#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Globalization;
using System.Text;

namespace YokiFrame
{
    /// <summary>提供 LogKit 固定设置 parser 使用的严格 JSON primitive reader。</summary>
    internal static class LogKitSettingsJsonReader
    {
        /// <summary>读取标准 JSON 字符串，完整支持 unicode escape 和代理对。</summary>
        internal static bool TryReadString(
            string json,
            ref int index,
            out string value,
            out string errorMessage)
        {
            value = null;
            errorMessage = string.Empty;
            if (!Consume(json, ref index, '"'))
            {
                return false;
            }

            var builder = new StringBuilder();
            while (index < json.Length)
            {
                char current = json[index++];
                if (current == '"')
                {
                    value = builder.ToString();
                    return true;
                }

                if (current < ' ')
                {
                    errorMessage = "JSON strings cannot contain raw control characters.";
                    return false;
                }

                if (current == '\\')
                {
                    if (!TryAppendEscape(json, ref index, builder, out errorMessage)) return false;
                    continue;
                }

                if (!TryAppendRawCharacter(json, ref index, current, builder, out errorMessage))
                {
                    return false;
                }
            }

            errorMessage = "JSON string is missing its closing quote.";
            return false;
        }

        /// <summary>追加原始字符，并拒绝未配对的 UTF-16 代理。</summary>
        private static bool TryAppendRawCharacter(
            string json,
            ref int index,
            char current,
            StringBuilder builder,
            out string errorMessage)
        {
            if (char.IsLowSurrogate(current)
                || (char.IsHighSurrogate(current)
                    && (index >= json.Length || !char.IsLowSurrogate(json[index]))))
            {
                errorMessage = "JSON string contains an unpaired UTF-16 surrogate.";
                return false;
            }

            builder.Append(current);
            if (char.IsHighSurrogate(current))
            {
                builder.Append(json[index++]);
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>读取严格小写 JSON boolean，不接受字符串或大小写变体。</summary>
        internal static bool TryReadBoolean(string json, ref int index, out bool value)
        {
            value = false;
            if (Matches(json, index, "true"))
            {
                index += 4;
                value = true;
                return true;
            }

            if (!Matches(json, index, "false"))
            {
                return false;
            }

            index += 5;
            return true;
        }

        /// <summary>读取 JSON 整数并拒绝前导零、小数、指数和 int 溢出。</summary>
        internal static bool TryReadInteger(string json, ref int index, out int value)
        {
            value = 0;
            int start = index;
            if (index < json.Length && json[index] == '-')
            {
                index++;
            }

            if (index >= json.Length || json[index] < '0' || json[index] > '9')
            {
                return false;
            }

            if (json[index] == '0' && index + 1 < json.Length
                && json[index + 1] >= '0' && json[index + 1] <= '9')
            {
                return false;
            }

            while (index < json.Length && json[index] >= '0' && json[index] <= '9')
            {
                index++;
            }

            string token = json.Substring(start, index - start);
            return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>当前位置匹配指定字符时消费它。</summary>
        internal static bool Consume(string json, ref int index, char expected)
        {
            if (index >= json.Length || json[index] != expected)
            {
                return false;
            }

            index++;
            return true;
        }

        /// <summary>跳过 JSON 允许的四种空白字符。</summary>
        internal static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length)
            {
                char current = json[index];
                if (current != ' ' && current != '\t' && current != '\r' && current != '\n') return;
                index++;
            }
        }

        /// <summary>读取一个标准 JSON escape，并拒绝未知转义。</summary>
        private static bool TryAppendEscape(
            string json,
            ref int index,
            StringBuilder builder,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (index >= json.Length)
            {
                errorMessage = "JSON string ends inside an escape sequence.";
                return false;
            }

            char escaped = json[index++];
            switch (escaped)
            {
                case '"': builder.Append('"'); return true;
                case '\\': builder.Append('\\'); return true;
                case '/': builder.Append('/'); return true;
                case 'b': builder.Append('\b'); return true;
                case 'f': builder.Append('\f'); return true;
                case 'n': builder.Append('\n'); return true;
                case 'r': builder.Append('\r'); return true;
                case 't': builder.Append('\t'); return true;
                case 'u': return TryAppendUnicode(json, ref index, builder, out errorMessage);
                default:
                    errorMessage = "JSON string contains an unsupported escape sequence.";
                    return false;
            }
        }

        /// <summary>读取 unicode escape；高代理必须紧跟一个低代理 escape。</summary>
        private static bool TryAppendUnicode(
            string json,
            ref int index,
            StringBuilder builder,
            out string errorMessage)
        {
            if (!TryReadHexQuad(json, ref index, out var value))
            {
                errorMessage = "JSON unicode escape must contain four hexadecimal digits.";
                return false;
            }

            char current = (char)value;
            if (char.IsLowSurrogate(current))
            {
                errorMessage = "JSON unicode escape contains an unpaired low surrogate.";
                return false;
            }

            if (!char.IsHighSurrogate(current))
            {
                builder.Append(current);
                errorMessage = string.Empty;
                return true;
            }

            if (!ConsumeUnicodePrefix(json, ref index)
                || !TryReadHexQuad(json, ref index, out var lowValue)
                || !char.IsLowSurrogate((char)lowValue))
            {
                errorMessage = "JSON unicode escape contains an unpaired high surrogate.";
                return false;
            }

            builder.Append(current);
            builder.Append((char)lowValue);
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>读取四个十六进制数字，不接受不足四位或非法字符。</summary>
        private static bool TryReadHexQuad(string json, ref int index, out int value)
        {
            value = 0;
            if (index + 4 > json.Length)
            {
                return false;
            }

            for (var offset = 0; offset < 4; offset++)
            {
                int digit = ParseHex(json[index++]);
                if (digit < 0)
                {
                    return false;
                }

                value = (value << 4) | digit;
            }

            return true;
        }

        /// <summary>把单个十六进制字符转换为数字。</summary>
        private static int ParseHex(char value)
        {
            if (value >= '0' && value <= '9') return value - '0';
            if (value >= 'a' && value <= 'f') return value - 'a' + 10;
            return value >= 'A' && value <= 'F' ? value - 'A' + 10 : -1;
        }

        /// <summary>读取代理对中第二段必须存在的反斜杠和 u。</summary>
        private static bool ConsumeUnicodePrefix(string json, ref int index)
        {
            if (index + 2 > json.Length || json[index] != '\\' || json[index + 1] != 'u')
            {
                return false;
            }

            index += 2;
            return true;
        }

        /// <summary>判断当前位置是否匹配固定 primitive 文本。</summary>
        private static bool Matches(string json, int index, string token)
        {
            return index + token.Length <= json.Length
                && string.CompareOrdinal(json, index, token, 0, token.Length) == 0;
        }
    }
}
#endif
