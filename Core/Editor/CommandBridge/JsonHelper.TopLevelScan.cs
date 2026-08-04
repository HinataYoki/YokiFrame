#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 提供 JsonHelper 与危险命令确认共用的顶层对象扫描原语，保证字段定位不会命中字符串值或嵌套结构。
    /// </summary>
    public static partial class JsonHelper
    {
        /// <summary>
        /// 验证文本是完整 JSON 值；语法状态机位于独立纯 C# 校验器中。
        /// </summary>
        /// <param name="json">待验证 JSON 文本。</param>
        /// <exception cref="FormatException">JSON 语法不完整或包含非法 token 时抛出。</exception>
        internal static void EnsureValidJson(string json)
        {
            YokiFrameJsonSyntaxValidator.EnsureValidJson(json);
        }

        /// <summary>
        /// 在顶层 JSON 对象中定位字段值起始位置；跳过字符串值与嵌套对象、数组，重复字段按第一次出现处理。
        /// </summary>
        /// <param name="json">JSON 文本。</param>
        /// <param name="fieldName">顶层字段名。</param>
        /// <param name="valueStart">字段值第一个非空白字符位置。</param>
        /// <returns>顶层存在该字段且值非空时返回 true。</returns>
        internal static bool TryFindTopLevelValue(string json, string fieldName, out int valueStart)
        {
            valueStart = -1;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName))
            {
                return false;
            }

            var index = 0;
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != '{')
            {
                return false;
            }

            index++;
            return TryScanTopLevelProperties(json, fieldName, ref index, out valueStart);
        }

        /// <summary>
        /// 顺序扫描顶层属性，直到命中目标字段或对象结束；属性名含转义时保守终止扫描。
        /// </summary>
        /// <param name="json">JSON 文本。</param>
        /// <param name="fieldName">顶层字段名。</param>
        /// <param name="index">当前扫描位置。</param>
        /// <param name="valueStart">字段值第一个非空白字符位置。</param>
        /// <returns>命中目标字段时返回 true。</returns>
        private static bool TryScanTopLevelProperties(
            string json,
            string fieldName,
            ref int index,
            out int valueStart)
        {
            valueStart = -1;
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

                if (propertyName == fieldName)
                {
                    valueStart = index;
                    return index < json.Length;
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
        /// <param name="json">JSON 文本。</param>
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
        /// <param name="json">JSON 文本。</param>
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
        /// 跳过非目标属性的 JSON 值，支持字符串、对象、数组和字面量。
        /// </summary>
        /// <param name="json">JSON 文本。</param>
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
        /// <param name="json">JSON 文本。</param>
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
        /// 跳过嵌套对象或数组，保证顶层字段查找不会被嵌套同名字段干扰。
        /// </summary>
        /// <param name="json">JSON 文本。</param>
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
        /// <param name="json">JSON 文本。</param>
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
        /// 判断字符是否可以结束当前 JSON 值。
        /// </summary>
        /// <param name="value">待检查字符。</param>
        /// <returns>能结束当前值时返回 true。</returns>
        internal static bool IsValueTerminator(char value)
        {
            return value == ',' || value == '}' || value == ']' || char.IsWhiteSpace(value);
        }

        /// <summary>
        /// 跳过 JSON 允许的空白字符。
        /// </summary>
        /// <param name="json">JSON 文本。</param>
        /// <param name="index">当前扫描位置。</param>
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
