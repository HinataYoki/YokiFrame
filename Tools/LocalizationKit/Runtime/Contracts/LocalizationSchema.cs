using System;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>集中定义 LocalizationKit JSON v1 使用的语言、复数分类和格式版本约束。</summary>
    internal static class LocalizationSchema
    {
        /// <summary>当前 Runtime 支持的 JSON 格式版本。</summary>
        internal const int CurrentFormatVersion = 1;

        /// <summary>解析名称或数字形式的语言标识，并拒绝未定义的枚举值。</summary>
        /// <param name="value">JSON 中的语言文本。</param>
        /// <param name="languageId">解析成功后的规范语言标识。</param>
        /// <returns>输入属于公开语言枚举时返回 true。</returns>
        /// <remarks>先拒绝逗号，避免 Enum.TryParse 把名称列表按位或成另一个合法枚举值。</remarks>
        internal static bool TryParseLanguageId(string value, out LanguageId languageId)
        {
            languageId = default;
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(',') < 0
                && Enum.TryParse(value, true, out languageId)
                && Enum.IsDefined(typeof(LanguageId), languageId);
        }

        /// <summary>解析数字形式的语言标识，并拒绝未定义的枚举值。</summary>
        /// <param name="value">JSON 中的语言数字。</param>
        /// <param name="languageId">解析成功后的规范语言标识。</param>
        /// <returns>输入属于公开语言枚举时返回 true。</returns>
        internal static bool TryParseLanguageId(int value, out LanguageId languageId)
        {
            languageId = (LanguageId)value;
            return Enum.IsDefined(typeof(LanguageId), languageId);
        }

        /// <summary>解析名称或数字形式的复数分类，并拒绝未定义的枚举值。</summary>
        /// <param name="value">JSON 对象键中的复数分类文本。</param>
        /// <param name="category">解析成功后的规范复数分类。</param>
        /// <returns>输入属于公开复数分类枚举时返回 true。</returns>
        internal static bool TryParsePluralCategory(string value, out PluralCategory category)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                category = default;
                return false;
            }

            int numericValue;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out numericValue))
            {
                return TryParsePluralCategory(numericValue, out category);
            }

            category = default;
            return value.IndexOf(',') < 0
                && Enum.TryParse(value, true, out category)
                && Enum.IsDefined(typeof(PluralCategory), category);
        }

        /// <summary>解析数字形式的复数分类，并拒绝未定义的枚举值。</summary>
        /// <param name="value">JSON 中的复数分类数字。</param>
        /// <param name="category">解析成功后的规范复数分类。</param>
        /// <returns>输入属于公开复数分类枚举时返回 true。</returns>
        internal static bool TryParsePluralCategory(int value, out PluralCategory category)
        {
            category = (PluralCategory)value;
            return Enum.IsDefined(typeof(PluralCategory), category);
        }
    }
}
