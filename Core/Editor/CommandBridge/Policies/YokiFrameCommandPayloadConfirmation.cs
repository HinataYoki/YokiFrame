#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 解析 CommandBridge payload 中用于危险命令的顶层 confirmed 布尔确认。
    /// </summary>
    internal static class YokiFrameCommandPayloadConfirmation
    {
        private const string CONFIRMED_PROPERTY = "confirmed";
        private const string TRUE_LITERAL = "true";

        /// <summary>
        /// 判断 payload 是否包含顶层 JSON 布尔字段 confirmed=true。
        /// </summary>
        /// <param name="payloadJson">payload JSON 文本。</param>
        /// <returns>存在顶层 confirmed 布尔 true 时返回 true。</returns>
        public static bool HasConfirmedTrue(string payloadJson)
        {
            if (string.IsNullOrEmpty(payloadJson))
            {
                return false;
            }

            var index = 0;
            SkipWhitespace(payloadJson, ref index);
            if (index >= payloadJson.Length || payloadJson[index] != '{')
            {
                return false;
            }

            index++;
            return TryFindConfirmedProperty(payloadJson, ref index);
        }

        /// <summary>
        /// 在顶层对象中查找 confirmed 字段；重复字段按第一次出现处理，避免歧义绕过。
        /// </summary>
        /// <param name="json">payload JSON 文本。</param>
        /// <param name="index">当前扫描位置。</param>
        /// <returns>confirmed 字段为布尔 true 时返回 true。</returns>
        private static bool TryFindConfirmedProperty(string json, ref int index)
        {
            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == '}')
                {
                    return false;
                }

                if (!TryReadPropertyName(json, ref index, out var propertyName)
                    || !TrySkipPropertySeparator(json, ref index))
                {
                    return false;
                }

                if (propertyName == CONFIRMED_PROPERTY)
                {
                    return TryReadTrueLiteral(json, index);
                }

                if (!TrySkipValue(json, ref index))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',')
                {
                    index++;
                    continue;
                }

                return false;
            }

            return false;
        }

        /// <summary>
        /// 读取简单 JSON 属性名；包含转义的属性名保守判为不匹配。
        /// </summary>
        /// <param name="json">payload JSON 文本。</param>
        /// <param name="index">当前扫描位置。</param>
        /// <param name="propertyName">读取到的属性名。</param>
        /// <returns>读取成功时返回 true。</returns>
        private static bool TryReadPropertyName(string json, ref int index, out string propertyName)
        {
            propertyName = string.Empty;
            if (index >= json.Length || json[index] != '"')
            {
                return false;
            }

            var start = ++index;
            while (index < json.Length)
            {
                if (json[index] == '\\')
                {
                    return false;
                }

                if (json[index] == '"')
                {
                    propertyName = json.Substring(start, index - start);
                    index++;
                    return true;
                }

                index++;
            }

            return false;
        }

        /// <summary>
        /// 跳过属性名和值之间的冒号分隔符。
        /// </summary>
        /// <param name="json">payload JSON 文本。</param>
        /// <param name="index">当前扫描位置。</param>
        /// <returns>存在合法冒号时返回 true。</returns>
        private static bool TrySkipPropertySeparator(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != ':')
            {
                return false;
            }

            index++;
            SkipWhitespace(json, ref index);
            return true;
        }

        /// <summary>
        /// 读取 JSON 布尔 true，并要求后续字符能结束当前值。
        /// </summary>
        /// <param name="json">payload JSON 文本。</param>
        /// <param name="index">当前扫描位置。</param>
        /// <returns>当前位置是布尔 true 时返回 true。</returns>
        private static bool TryReadTrueLiteral(string json, int index)
        {
            if (index + TRUE_LITERAL.Length > json.Length)
            {
                return false;
            }

            if (string.CompareOrdinal(json, index, TRUE_LITERAL, 0, TRUE_LITERAL.Length) != 0)
            {
                return false;
            }

            var nextIndex = index + TRUE_LITERAL.Length;
            return nextIndex >= json.Length || IsValueTerminator(json[nextIndex]);
        }

        /// <summary>
        /// 跳过非 confirmed 属性的 JSON 值，支持字符串、对象、数组和字面量。
        /// </summary>
        /// <param name="json">payload JSON 文本。</param>
        /// <param name="index">当前扫描位置。</param>
        /// <returns>成功跳过一个值时返回 true。</returns>
        private static bool TrySkipValue(string json, ref int index)
        {
            if (index >= json.Length)
            {
                return false;
            }

            if (json[index] == '"')
            {
                return TrySkipString(json, ref index);
            }

            if (json[index] == '{' || json[index] == '[')
            {
                return TrySkipObjectOrArray(json, ref index);
            }

            return TrySkipLiteralOrNumber(json, ref index);
        }

        /// <summary>
        /// 跳过 JSON 字符串值；转义字符只作为边界处理，不做语义解析。
        /// </summary>
        /// <param name="json">payload JSON 文本。</param>
        /// <param name="index">当前扫描位置。</param>
        /// <returns>成功跳过字符串时返回 true。</returns>
        private static bool TrySkipString(string json, ref int index)
        {
            index++;
            while (index < json.Length)
            {
                if (json[index] == '\\')
                {
                    index += 2;
                    continue;
                }

                if (json[index] == '"')
                {
                    index++;
                    return true;
                }

                index++;
            }

            return false;
        }

        /// <summary>
        /// 跳过嵌套对象或数组，保证顶层 confirmed 查找不会被嵌套字段干扰。
        /// </summary>
        /// <param name="json">payload JSON 文本。</param>
        /// <param name="index">当前扫描位置。</param>
        /// <returns>成功跳过嵌套结构时返回 true。</returns>
        private static bool TrySkipObjectOrArray(string json, ref int index)
        {
            var depth = 0;
            while (index < json.Length)
            {
                if (json[index] == '"')
                {
                    if (!TrySkipString(json, ref index))
                    {
                        return false;
                    }

                    continue;
                }

                if (json[index] == '{' || json[index] == '[')
                {
                    depth++;
                }
                else if (json[index] == '}' || json[index] == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        index++;
                        return true;
                    }
                }

                index++;
            }

            return false;
        }

        /// <summary>
        /// 跳过数字、false、true、null 等非字符串简单值。
        /// </summary>
        /// <param name="json">payload JSON 文本。</param>
        /// <param name="index">当前扫描位置。</param>
        /// <returns>至少跳过一个字符时返回 true。</returns>
        private static bool TrySkipLiteralOrNumber(string json, ref int index)
        {
            var start = index;
            while (index < json.Length && !IsValueTerminator(json[index]))
            {
                index++;
            }

            return index > start;
        }

        /// <summary>
        /// 跳过 JSON 允许的空白字符。
        /// </summary>
        /// <param name="json">payload JSON 文本。</param>
        /// <param name="index">当前扫描位置。</param>
        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }
        }

        /// <summary>
        /// 判断字符是否可以结束当前 JSON 值。
        /// </summary>
        /// <param name="value">待检查字符。</param>
        /// <returns>能结束当前值时返回 true。</returns>
        private static bool IsValueTerminator(char value)
        {
            return value == ',' || value == '}' || value == ']' || char.IsWhiteSpace(value);
        }
    }
}
#endif
