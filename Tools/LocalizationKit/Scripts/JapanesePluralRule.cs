namespace YokiFrame
{
    /// <summary>
    /// 日文复数规则
    /// 日文没有复数变化，所有数量都返回 Other
    /// </summary>
    public sealed class JapanesePluralRule : IPluralRule
    {
        public static readonly JapanesePluralRule Instance = new();

        public LanguageId LanguageId => LanguageId.Japanese;

        private JapanesePluralRule() { }

        public PluralCategory GetCategory(int count) => PluralCategory.Other;

        public PluralCategory GetCategory(double count) => PluralCategory.Other;
    }
}
