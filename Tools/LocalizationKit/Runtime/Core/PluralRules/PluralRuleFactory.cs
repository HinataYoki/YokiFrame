using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 复数规则工厂
    /// 根据语言获取对应的复数规则实例
    /// </summary>
    public static class PluralRuleFactory
    {
        private static readonly Dictionary<LanguageId, IPluralRule> sRules = new()
        {
            { LanguageId.ChineseSimplified, ChinesePluralRule.SimplifiedInstance },
            { LanguageId.ChineseTraditional, ChinesePluralRule.TraditionalInstance },
            { LanguageId.English, EnglishPluralRule.Instance },
            { LanguageId.Japanese, JapanesePluralRule.Instance },
            { LanguageId.Korean, KoreanPluralRule.Instance },
        };

        // 默认规则（无复数变化）
        private static readonly IPluralRule sDefaultRule = ChinesePluralRule.SimplifiedInstance;

        /// <summary>
        /// 获取指定语言的复数规则
        /// </summary>
        /// <param name="languageId">语言标识符</param>
        /// <returns>复数规则实例</returns>
        public static IPluralRule GetRule(LanguageId languageId)
        {
            return sRules.TryGetValue(languageId, out var rule) ? rule : sDefaultRule;
        }

        /// <summary>
        /// 注册自定义复数规则
        /// </summary>
        /// <param name="rule">复数规则实例</param>
        public static void RegisterRule(IPluralRule rule)
        {
            sRules[rule.LanguageId] = rule;
        }

        /// <summary>
        /// 获取指定语言和数量的复数类别
        /// </summary>
        /// <param name="languageId">语言标识符</param>
        /// <param name="count">数量</param>
        /// <returns>复数类别</returns>
        public static PluralCategory GetCategory(LanguageId languageId, int count)
        {
            return GetRule(languageId).GetCategory(count);
        }

        /// <summary>
        /// 获取指定语言和数量的复数类别（支持小数）
        /// </summary>
        /// <param name="languageId">语言标识符</param>
        /// <param name="count">数量</param>
        /// <returns>复数类别</returns>
        public static PluralCategory GetCategory(LanguageId languageId, double count)
        {
            return GetRule(languageId).GetCategory(count);
        }
    }
}
