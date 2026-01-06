namespace YokiFrame
{
    /// <summary>
    /// 韩文复数规则
    /// 韩文没有复数变化，所有数量都返回 Other
    /// </summary>
    public sealed class KoreanPluralRule : IPluralRule
    {
        public static readonly KoreanPluralRule Instance = new();

        public LanguageId LanguageId => LanguageId.Korean;

        private KoreanPluralRule() { }

        public PluralCategory GetCategory(int count) => PluralCategory.Other;

        public PluralCategory GetCategory(double count) => PluralCategory.Other;
    }
}
