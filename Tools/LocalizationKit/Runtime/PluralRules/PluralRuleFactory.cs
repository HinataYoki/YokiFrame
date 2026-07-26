using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>管理语言复数规则，并为未注册语言提供安全的 Other fallback。</summary>
    public static class PluralRuleFactory
    {
        private static readonly KeyValuePair<LanguageId, IPluralRule>[] sBuiltinRules =
        {
            new KeyValuePair<LanguageId, IPluralRule>(LanguageId.ChineseSimplified, InvariantPluralRule.ChineseSimplified),
            new KeyValuePair<LanguageId, IPluralRule>(LanguageId.ChineseTraditional, InvariantPluralRule.ChineseTraditional),
            new KeyValuePair<LanguageId, IPluralRule>(LanguageId.English, EnglishPluralRule.Instance),
            new KeyValuePair<LanguageId, IPluralRule>(LanguageId.Japanese, InvariantPluralRule.Japanese),
            new KeyValuePair<LanguageId, IPluralRule>(LanguageId.Korean, InvariantPluralRule.Korean)
        };

        private static readonly Dictionary<LanguageId, IPluralRule> sRules = new Dictionary<LanguageId, IPluralRule>();

        private static readonly IPluralRule sDefaultRule = InvariantPluralRule.ChineseSimplified;

        /// <summary>初始化内置复数规则注册表。</summary>
        static PluralRuleFactory() => ResetRules();

        /// <summary>恢复内置复数规则，清除全部自定义注册。</summary>
        public static void ResetRules()
        {
            sRules.Clear();
            for (int index = 0; index < sBuiltinRules.Length; index++)
            {
                sRules[sBuiltinRules[index].Key] = sBuiltinRules[index].Value;
            }
        }

        /// <summary>获取指定语言的规则；未注册语言返回默认 invariant 规则。</summary>
        public static IPluralRule GetRule(LanguageId languageId)
        {
            IPluralRule rule;
            return sRules.TryGetValue(languageId, out rule) ? rule : sDefaultRule;
        }

        /// <summary>注册或替换指定语言的规则。</summary>
        /// <param name="rule">不得为空的规则实例。</param>
        public static void RegisterRule(IPluralRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            sRules[rule.LanguageId] = rule;
        }

        /// <summary>按整数数量获取复数分类。</summary>
        public static PluralCategory GetCategory(LanguageId languageId, int count) => GetRule(languageId).GetCategory(count);

        /// <summary>按浮点数量获取复数分类。</summary>
        public static PluralCategory GetCategory(LanguageId languageId, double count) => GetRule(languageId).GetCategory(count);
    }
}
