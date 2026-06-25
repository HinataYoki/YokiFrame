using System;

namespace YokiFrame
{
    internal readonly struct LocalizationTextCacheKey : IEquatable<LocalizationTextCacheKey>
    {
        private const int HASH_MULTIPLIER = 397;

        private readonly int mTextId;

        internal LocalizationTextCacheKey(LanguageId languageId, int textId)
        {
            LanguageId = languageId;
            mTextId = textId;
        }

        internal LanguageId LanguageId { get; }

        public bool Equals(LocalizationTextCacheKey other) =>
            LanguageId == other.LanguageId &&
            mTextId == other.mTextId;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is LocalizationTextCacheKey other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)LanguageId * HASH_MULTIPLIER) ^ mTextId;
            }
        }
    }
}
