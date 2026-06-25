using System;

namespace YokiFrame
{
    internal readonly struct LocalizationPluralCacheKey : IEquatable<LocalizationPluralCacheKey>
    {
        private const int HASH_MULTIPLIER = 397;

        private readonly int mTextId;
        private readonly PluralCategory mCategory;

        internal LocalizationPluralCacheKey(LanguageId languageId, int textId, PluralCategory category)
        {
            LanguageId = languageId;
            mTextId = textId;
            mCategory = category;
        }

        internal LanguageId LanguageId { get; }

        public bool Equals(LocalizationPluralCacheKey other) =>
            LanguageId == other.LanguageId &&
            mTextId == other.mTextId &&
            mCategory == other.mCategory;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is LocalizationPluralCacheKey other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)LanguageId;
                hash = (hash * HASH_MULTIPLIER) ^ mTextId;
                hash = (hash * HASH_MULTIPLIER) ^ (int)mCategory;
                return hash;
            }
        }
    }
}
