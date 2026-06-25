using System;

namespace YokiFrame
{
    /// <summary>
    /// 英语复数规则，数量为 1 时使用单数，其余使用 Other。
    /// </summary>
    public sealed class EnglishPluralRule : IPluralRule
    {
        private const double DOUBLE_ONE_TOLERANCE = 1e-9d;

        /// <summary>
        /// 获取英语复数规则单例。
        /// </summary>
        public static readonly EnglishPluralRule Instance = new EnglishPluralRule();

        private EnglishPluralRule()
        {
        }

        /// <inheritdoc />
        public LanguageId LanguageId => LanguageId.English;

        /// <inheritdoc />
        public PluralCategory GetCategory(int count) =>
            count == 1 ? PluralCategory.One : PluralCategory.Other;

        /// <inheritdoc />
        public PluralCategory GetCategory(double count) =>
            Math.Abs(count - 1.0d) <= DOUBLE_ONE_TOLERANCE ? PluralCategory.One : PluralCategory.Other;
    }
}
