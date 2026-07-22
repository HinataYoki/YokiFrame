namespace YokiFrame
{
    /// <summary>不区分复数形态、始终返回 Other 的语言规则。</summary>
    public sealed class InvariantPluralRule : IPluralRule
    {
        /// <summary>简体中文规则。</summary>
        public static readonly InvariantPluralRule ChineseSimplified = new InvariantPluralRule(LanguageId.ChineseSimplified);
        /// <summary>繁体中文规则。</summary>
        public static readonly InvariantPluralRule ChineseTraditional = new InvariantPluralRule(LanguageId.ChineseTraditional);
        /// <summary>日语规则。</summary>
        public static readonly InvariantPluralRule Japanese = new InvariantPluralRule(LanguageId.Japanese);
        /// <summary>韩语规则。</summary>
        public static readonly InvariantPluralRule Korean = new InvariantPluralRule(LanguageId.Korean);

        /// <summary>创建指定语言的 invariant 规则。</summary>
        public InvariantPluralRule(LanguageId languageId) => LanguageId = languageId;

        /// <inheritdoc />
        public LanguageId LanguageId { get; }

        /// <inheritdoc />
        public PluralCategory GetCategory(int count) => PluralCategory.Other;

        /// <inheritdoc />
        public PluralCategory GetCategory(double count) => PluralCategory.Other;
    }
}
