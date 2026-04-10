using System;

namespace YokiFrame
{
    /// <summary>
    /// 英文复数规则
    /// 1 = One, 其他 = Other
    /// </summary>
    public sealed class EnglishPluralRule : IPluralRule
    {
        public static readonly EnglishPluralRule Instance = new();

        public LanguageId LanguageId => LanguageId.English;

        private EnglishPluralRule() { }

        public PluralCategory GetCategory(int count)
        {
            return count == 1 ? PluralCategory.One : PluralCategory.Other;
        }

        public PluralCategory GetCategory(double count)
        {
            // 英文中只有整数 1 是单数
            return Math.Abs(count - 1.0) < double.Epsilon ? PluralCategory.One : PluralCategory.Other;
        }
    }
}
