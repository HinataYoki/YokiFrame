using System;
using System.Collections.Generic;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>
    /// 按 C# 9 标识符字符类别和保留关键字校验公开结构化名称。
    /// </summary>
    internal static class CSharpIdentifierValidator
    {
        private static readonly HashSet<string> sReservedKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum",
            "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto",
            "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
            "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
            "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
            "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
            "ushort", "using", "virtual", "void", "volatile", "while"
        };

        /// <summary>
        /// 验证单个标识符，允许使用 `@` 显式转义保留关键字。
        /// </summary>
        /// <param name="value">待验证标识符。</param>
        /// <param name="parameterName">异常中使用的参数名。</param>
        /// <returns>经过校验的原始标识符。</returns>
        internal static string RequireIdentifier(string value, string parameterName)
        {
            CSharpText.RequireNonEmptyLine(value, parameterName);
            int startIndex = value[0] == '@' ? 1 : 0;
            if (startIndex == value.Length || !IsIdentifierStart(value[startIndex]))
            {
                throw CreateException(value, parameterName);
            }

            for (var index = startIndex + 1; index < value.Length; index++)
            {
                if (!IsIdentifierPart(value[index]))
                {
                    throw CreateException(value, parameterName);
                }
            }

            if (startIndex == 0 && sReservedKeywords.Contains(value))
            {
                throw CreateException(value, parameterName);
            }

            return value;
        }

        /// <summary>
        /// 验证由点分隔的命名空间或特性类型名，每个片段都必须是合法标识符。
        /// </summary>
        /// <param name="value">待验证限定名称。</param>
        /// <param name="parameterName">异常中使用的参数名。</param>
        /// <returns>经过校验的原始限定名称。</returns>
        internal static string RequireQualifiedName(string value, string parameterName)
        {
            CSharpText.RequireNonEmptyLine(value, parameterName);
            string[] segments = value.Split('.');
            for (var index = 0; index < segments.Length; index++)
            {
                RequireIdentifier(segments[index], parameterName);
            }

            return value;
        }

        /// <summary>
        /// 判断字符是否符合 C# 标识符首字符类别。
        /// </summary>
        /// <param name="value">待判断字符。</param>
        /// <returns>允许作为首字符时返回 true。</returns>
        private static bool IsIdentifierStart(char value)
        {
            if (value == '_')
            {
                return true;
            }

            UnicodeCategory category = char.GetUnicodeCategory(value);
            return category == UnicodeCategory.UppercaseLetter
                || category == UnicodeCategory.LowercaseLetter
                || category == UnicodeCategory.TitlecaseLetter
                || category == UnicodeCategory.ModifierLetter
                || category == UnicodeCategory.OtherLetter
                || category == UnicodeCategory.LetterNumber;
        }

        /// <summary>
        /// 判断字符是否符合 C# 标识符后续字符类别。
        /// </summary>
        /// <param name="value">待判断字符。</param>
        /// <returns>允许作为后续字符时返回 true。</returns>
        private static bool IsIdentifierPart(char value)
        {
            if (IsIdentifierStart(value))
            {
                return true;
            }

            UnicodeCategory category = char.GetUnicodeCategory(value);
            return category == UnicodeCategory.DecimalDigitNumber
                || category == UnicodeCategory.ConnectorPunctuation
                || category == UnicodeCategory.NonSpacingMark
                || category == UnicodeCategory.SpacingCombiningMark
                || category == UnicodeCategory.Format;
        }

        /// <summary>
        /// 创建包含实际非法名称的参数异常，便于生成器定位配置来源。
        /// </summary>
        /// <param name="value">非法标识符。</param>
        /// <param name="parameterName">异常参数名。</param>
        /// <returns>可直接抛出的参数异常。</returns>
        private static ArgumentException CreateException(string value, string parameterName)
        {
            return new ArgumentException("不是合法的 C# 标识符或限定名称: " + value, parameterName);
        }
    }
}
