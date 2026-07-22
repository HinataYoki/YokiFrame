using System;

namespace YokiFrame
{
    /// <summary>英语复数规则，只有数量 1 使用 One。</summary>
    public sealed class EnglishPluralRule : IPluralRule
    {
        private const double DOUBLE_ONE_TOLERANCE = 1e-9d;

        /// <summary>获取英语规则单例。</summary>
        public static readonly EnglishPluralRule Instance = new EnglishPluralRule();

        /// <summary>阻止重复创建无状态规则实例。</summary>
        private EnglishPluralRule()
        {
        }

        /// <inheritdoc />
        public LanguageId LanguageId => LanguageId.English;

        /// <inheritdoc />
        public PluralCategory GetCategory(int count) => count == 1 ? PluralCategory.One : PluralCategory.Other;

        /// <inheritdoc />
        public PluralCategory GetCategory(double count) =>
            Math.Abs(count - 1d) <= DOUBLE_ONE_TOLERANCE ? PluralCategory.One : PluralCategory.Other;
    }
}
