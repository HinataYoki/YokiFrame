namespace YokiFrame
{
    /// <summary>
    /// 不区分复数形态的语言规则，始终返回 <see cref="PluralCategory.Other"/>。
    /// </summary>
    public sealed class InvariantPluralRule : IPluralRule
    {
        /// <summary>
        /// 简体中文复数规则。
        /// </summary>
        public static readonly InvariantPluralRule ChineseSimplified = new InvariantPluralRule(LanguageId.ChineseSimplified);

        /// <summary>
        /// 繁体中文复数规则。
        /// </summary>
        public static readonly InvariantPluralRule ChineseTraditional = new InvariantPluralRule(LanguageId.ChineseTraditional);

        /// <summary>
        /// 日语复数规则。
        /// </summary>
        public static readonly InvariantPluralRule Japanese = new InvariantPluralRule(LanguageId.Japanese);

        /// <summary>
        /// 韩语复数规则。
        /// </summary>
        public static readonly InvariantPluralRule Korean = new InvariantPluralRule(LanguageId.Korean);

        /// <summary>
        /// 创建不区分复数形态的语言规则。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        public InvariantPluralRule(LanguageId languageId)
        {
            LanguageId = languageId;
        }

        /// <inheritdoc />
        public LanguageId LanguageId { get; private set; }

        /// <inheritdoc />
        public PluralCategory GetCategory(int _) => PluralCategory.Other;

        /// <inheritdoc />
        public PluralCategory GetCategory(double _) => PluralCategory.Other;
    }
}
