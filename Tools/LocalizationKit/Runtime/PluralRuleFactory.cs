using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 管理语言到复数规则的映射，并提供复数分类查询入口。
    /// </summary>
    public static class PluralRuleFactory
    {
        private static readonly Dictionary<LanguageId, IPluralRule> sRules = new()
        {
            { LanguageId.ChineseSimplified, InvariantPluralRule.ChineseSimplified },
            { LanguageId.ChineseTraditional, InvariantPluralRule.ChineseTraditional },
            { LanguageId.English, EnglishPluralRule.Instance },
            { LanguageId.Japanese, InvariantPluralRule.Japanese },
            { LanguageId.Korean, InvariantPluralRule.Korean }
        };

        private static readonly IPluralRule sDefaultRule = InvariantPluralRule.ChineseSimplified;

        /// <summary>
        /// 获取指定语言的复数规则。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        /// <returns>复数规则；未注册时返回默认规则。</returns>
        public static IPluralRule GetRule(LanguageId languageId)
        {
            IPluralRule rule;
            return sRules.TryGetValue(languageId, out rule) ? rule : sDefaultRule;
        }

        /// <summary>
        /// 注册或替换指定语言的复数规则。
        /// </summary>
        /// <param name="rule">复数规则。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="rule"/> 为空时抛出。</exception>
        public static void RegisterRule(IPluralRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            sRules[rule.LanguageId] = rule;
        }

        /// <summary>
        /// 根据整数数量获取指定语言的复数分类。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        /// <param name="count">数量。</param>
        /// <returns>复数分类。</returns>
        public static PluralCategory GetCategory(LanguageId languageId, int count) =>
            GetRule(languageId).GetCategory(count);

        /// <summary>
        /// 根据浮点数量获取指定语言的复数分类。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        /// <param name="count">数量。</param>
        /// <returns>复数分类。</returns>
        public static PluralCategory GetCategory(LanguageId languageId, double count) =>
            GetRule(languageId).GetCategory(count);
    }
}
