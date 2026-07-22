#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Globalization;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 提供 Core 协议使用的轻量 JSON 辅助方法，只处理扁平对象和简单嵌套片段。
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// 从 JSON 对象中提取字符串字段。
        /// </summary>
        /// <param name="json">JSON 文本。</param>
        /// <param name="fieldName">字段名。</param>
        /// <returns>字段值；不存在或不是字符串时返回 null。</returns>
        public static string ExtractString(string json, string fieldName)
        {
            int index = FindValueStart(json, fieldName);
            if (index < 0 || json[index] != '"')
            {
                return null;
            }

            return ReadQuotedString(json, index);
        }

        /// <summary>
        /// 从 JSON 对象中提取 int 字段，支持数字值和字符串数字值。
        /// </summary>
        /// <param name="json">JSON 文本。</param>
        /// <param name="fieldName">字段名。</param>
        /// <param name="value">解析成功时返回的整数。</param>
        /// <returns>解析成功时返回 true。</returns>
        public static bool TryExtractInt(string json, string fieldName, out int value)
        {
            value = 0;
            string stringValue = ExtractString(json, fieldName);
            if (stringValue != null)
            {
                return int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }

            string primitiveValue = ExtractPrimitive(json, fieldName);
            return primitiveValue != null && int.TryParse(primitiveValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// 从 JSON 对象中提取 long 字段，支持数字值和字符串数字值。
        /// </summary>
        /// <param name="json">JSON 文本。</param>
        /// <param name="fieldName">字段名。</param>
        /// <param name="value">解析成功时返回的长整数。</param>
        /// <returns>解析成功时返回 true。</returns>
        public static bool TryExtractLong(string json, string fieldName, out long value)
        {
            value = 0L;
            string stringValue = ExtractString(json, fieldName);
            if (stringValue != null)
            {
                return long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }

            string primitiveValue = ExtractPrimitive(json, fieldName);
            return primitiveValue != null
                && long.TryParse(primitiveValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// 从 JSON 对象中提取 float 字段，支持数字值和字符串数字值。
        /// </summary>
        /// <param name="json">JSON 文本。</param>
        /// <param name="fieldName">字段名。</param>
        /// <param name="value">解析成功时返回的有限浮点数。</param>
        /// <returns>字段存在、格式正确且数值有限时返回 true。</returns>
        public static bool TryExtractFloat(string json, string fieldName, out float value)
        {
            value = 0f;
            string text = ExtractString(json, fieldName) ?? ExtractPrimitive(json, fieldName);
            return text != null
                && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !float.IsNaN(value)
                && !float.IsInfinity(value);
        }

        /// <summary>
        /// 从 JSON 对象中提取 bool 字段，支持布尔值和字符串布尔值。
        /// </summary>
        /// <param name="json">JSON 文本。</param>
        /// <param name="fieldName">字段名。</param>
        /// <param name="value">解析成功时返回的布尔值。</param>
        /// <returns>解析成功时返回 true。</returns>
        public static bool TryExtractBool(string json, string fieldName, out bool value)
        {
            value = false;
            string stringValue = ExtractString(json, fieldName);
            if (stringValue != null)
            {
                return bool.TryParse(stringValue, out value);
            }

            string primitiveValue = ExtractPrimitive(json, fieldName);
            return TryParseBoolPrimitive(primitiveValue, out value);
        }

        /// <summary>
        /// 转义字符串，使其可安全嵌入 JSON 字符串字面量。
        /// </summary>
        /// <param name="value">原始字符串。</param>
        /// <returns>已转义字符串。</returns>
        public static string EscapeString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length + 8);
            AppendEscapedString(builder, value);
            return builder.ToString();
        }

        /// <summary>
        /// 将 JSON 转义文本直接追加到既有缓冲区，避免状态序列化为每个字段创建中间字符串。
        /// </summary>
        /// <param name="builder">接收转义文本的目标缓冲区。</param>
        /// <param name="value">待转义的原始字符串。</param>
        internal static void AppendEscapedString(StringBuilder builder, string value)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            for (var index = 0; index < value.Length; index++)
            {
                AppendEscapedChar(builder, value[index]);
            }
        }

        /// <summary>
        /// 查找字段值起始位置。
        /// </summary>
        /// <param name="json">JSON 文本。</param>
        /// <param name="fieldName">字段名。</param>
        /// <returns>值起始位置；找不到时返回 -1。</returns>
        private static int FindValueStart(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName))
            {
                return -1;
            }

            string search = "\"" + fieldName + "\"";
            int index = json.IndexOf(search, StringComparison.Ordinal);
            if (index < 0)
            {
                return -1;
            }

            index += search.Length;
            SkipJsonSeparators(json, ref index);
            return index < json.Length ? index : -1;
        }

        /// <summary>
        /// 跳过字段名和值之间的空白和冒号。
        /// </summary>
        /// <param name="json">JSON 文本。</param>
        /// <param name="index">当前位置引用。</param>
        private static void SkipJsonSeparators(string json, ref int index)
        {
            while (index < json.Length && (json[index] == ' ' || json[index] == ':' || json[index] == '\t' || json[index] == '\r' || json[index] == '\n'))
            {
                index++;
            }
        }

        /// <summary>
        /// 读取简单 JSON 字符串值，并处理常用转义字符。
        /// </summary>
        /// <param name="json">JSON 文本。</param>
        /// <param name="quoteIndex">起始引号位置。</param>
        /// <returns>字符串值；格式无效时返回 null。</returns>
        private static string ReadQuotedString(string json, int quoteIndex)
        {
            var builder = new StringBuilder();
            for (var index = quoteIndex + 1; index < json.Length; index++)
            {
                char current = json[index];
                if (current == '"')
                {
                    return builder.ToString();
                }

                AppendJsonStringChar(builder, json, ref index, current);
            }

            return null;
        }

        /// <summary>
        /// 追加 JSON 字符串中的普通字符或转义字符。
        /// </summary>
        /// <param name="builder">字符串构建器。</param>
        /// <param name="json">JSON 文本。</param>
        /// <param name="index">当前位置引用。</param>
        /// <param name="current">当前字符。</param>
        private static void AppendJsonStringChar(StringBuilder builder, string json, ref int index, char current)
        {
            if (current != '\\' || index + 1 >= json.Length)
            {
                builder.Append(current);
                return;
            }

            index++;
            AppendEscapedJsonValue(builder, json[index]);
        }

        /// <summary>
        /// 追加 JSON 字符串转义后的实际字符。
        /// </summary>
        /// <param name="builder">字符串构建器。</param>
        /// <param name="escaped">转义标识字符。</param>
        private static void AppendEscapedJsonValue(StringBuilder builder, char escaped)
        {
            switch (escaped)
            {
                case '"': builder.Append('"'); break;
                case '\\': builder.Append('\\'); break;
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                default: builder.Append(escaped); break;
            }
        }

        /// <summary>
        /// 提取 JSON primitive 值。
        /// </summary>
        /// <param name="json">JSON 文本。</param>
        /// <param name="fieldName">字段名。</param>
        /// <returns>primitive 文本；不存在时返回 null。</returns>
        private static string ExtractPrimitive(string json, string fieldName)
        {
            int index = FindValueStart(json, fieldName);
            if (index < 0 || json[index] == '"' || json[index] == '{' || json[index] == '[')
            {
                return null;
            }

            int start = index;
            while (index < json.Length && !IsPrimitiveTerminator(json[index]))
            {
                index++;
            }

            return index > start ? json.Substring(start, index - start) : null;
        }

        /// <summary>
        /// 判断字符是否表示 primitive 值结束。
        /// </summary>
        /// <param name="value">待判断字符。</param>
        /// <returns>结束字符时返回 true。</returns>
        private static bool IsPrimitiveTerminator(char value)
        {
            return value == ',' || value == '}' || value == ']' || value == ' ' || value == '\t' || value == '\r' || value == '\n';
        }

        /// <summary>
        /// 解析 primitive 布尔值。
        /// </summary>
        /// <param name="primitiveValue">primitive 文本。</param>
        /// <param name="value">解析结果。</param>
        /// <returns>解析成功时返回 true。</returns>
        private static bool TryParseBoolPrimitive(string primitiveValue, out bool value)
        {
            value = false;
            if (string.Equals(primitiveValue, "true", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            return string.Equals(primitiveValue, "false", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 追加 JSON 转义字符。
        /// </summary>
        /// <param name="builder">字符串构建器。</param>
        /// <param name="value">原始字符。</param>
        private static void AppendEscapedChar(StringBuilder builder, char value)
        {
            switch (value)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default: AppendDefaultEscapedChar(builder, value); break;
            }
        }

        /// <summary>
        /// 追加普通字符或 unicode 转义。
        /// </summary>
        /// <param name="builder">字符串构建器。</param>
        /// <param name="value">原始字符。</param>
        private static void AppendDefaultEscapedChar(StringBuilder builder, char value)
        {
            if (value < ' ')
            {
                builder.Append("\\u");
                builder.Append(((int)value).ToString("x4", CultureInfo.InvariantCulture));
                return;
            }

            builder.Append(value);
        }
    }
}
#endif
