namespace YokiFrame
{
    /// <summary>
    /// 中文复数规则
    /// 中文没有复数变化，所有数量都返回 Other
    /// </summary>
    public sealed class ChinesePluralRule : IPluralRule
    {
        public static readonly ChinesePluralRule SimplifiedInstance = new(LanguageId.ChineseSimplified);
        public static readonly ChinesePluralRule TraditionalInstance = new(LanguageId.ChineseTraditional);

        public LanguageId LanguageId { get; }

        private ChinesePluralRule(LanguageId languageId)
        {
            LanguageId = languageId;
        }

        public PluralCategory GetCategory(int count) => PluralCategory.Other;

        public PluralCategory GetCategory(double count) => PluralCategory.Other;
    }
}
