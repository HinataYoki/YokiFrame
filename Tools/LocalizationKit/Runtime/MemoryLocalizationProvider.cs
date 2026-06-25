using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 基于内存字典的本地化数据提供器，适合测试、原型和轻量运行时数据。
    /// </summary>
    public sealed class MemoryLocalizationProvider : ILocalizationProvider
    {
        private readonly Dictionary<LanguageId, Dictionary<int, string>> mTexts = new();

        private readonly Dictionary<LanguageId, Dictionary<int, Dictionary<PluralCategory, string>>> mPluralTexts = new();

        private readonly Dictionary<LanguageId, LanguageInfo> mLanguageInfos = new();

        private readonly List<LanguageId> mSupportedLanguages = new();
        private readonly HashSet<LanguageId> mSupportedLanguageSet = new();
        private readonly HashSet<LanguageId> mUnloadedLanguages = new();

        /// <summary>
        /// 设置指定语言的普通文本。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        /// <param name="textId">文本编号。</param>
        /// <param name="text">文本内容。</param>
        /// <returns>当前提供器实例。</returns>
        public MemoryLocalizationProvider SetText(LanguageId languageId, int textId, string text)
        {
            EnsureLanguage(languageId);

            Dictionary<int, string> languageTexts;
            if (!mTexts.TryGetValue(languageId, out languageTexts))
            {
                languageTexts = new();
                mTexts.Add(languageId, languageTexts);
            }

            languageTexts[textId] = text;
            mUnloadedLanguages.Remove(languageId);
            return this;
        }

        /// <summary>
        /// 设置指定语言和复数分类的文本。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        /// <param name="textId">文本编号。</param>
        /// <param name="category">复数分类。</param>
        /// <param name="text">文本内容。</param>
        /// <returns>当前提供器实例。</returns>
        public MemoryLocalizationProvider SetPluralText(LanguageId languageId, int textId, PluralCategory category, string text)
        {
            EnsureLanguage(languageId);

            Dictionary<int, Dictionary<PluralCategory, string>> languagePluralTexts;
            if (!mPluralTexts.TryGetValue(languageId, out languagePluralTexts))
            {
                languagePluralTexts = new Dictionary<int, Dictionary<PluralCategory, string>>();
                mPluralTexts.Add(languageId, languagePluralTexts);
            }

            Dictionary<PluralCategory, string> categoryTexts;
            if (!languagePluralTexts.TryGetValue(textId, out categoryTexts))
            {
                categoryTexts = new();
                languagePluralTexts.Add(textId, categoryTexts);
            }

            categoryTexts[category] = text;
            mUnloadedLanguages.Remove(languageId);
            return this;
        }

        /// <summary>
        /// 设置语言显示信息。
        /// </summary>
        /// <param name="languageInfo">语言显示信息。</param>
        /// <returns>当前提供器实例。</returns>
        public MemoryLocalizationProvider SetLanguageInfo(LanguageInfo languageInfo)
        {
            EnsureLanguage(languageInfo.Id);
            mLanguageInfos[languageInfo.Id] = languageInfo;
            return this;
        }

        /// <inheritdoc />
        public IReadOnlyList<LanguageId> GetSupportedLanguages() => mSupportedLanguages;

        /// <inheritdoc />
        public bool TryGetText(LanguageId languageId, int textId, out string text)
        {
            text = null;
            if (mUnloadedLanguages.Contains(languageId))
            {
                return false;
            }

            Dictionary<int, string> languageTexts;
            return mTexts.TryGetValue(languageId, out languageTexts) && languageTexts.TryGetValue(textId, out text);
        }

        /// <inheritdoc />
        public bool TryGetPluralText(LanguageId languageId, int textId, PluralCategory category, out string text)
        {
            text = null;
            if (mUnloadedLanguages.Contains(languageId))
            {
                return false;
            }

            Dictionary<int, Dictionary<PluralCategory, string>> languagePluralTexts;
            Dictionary<PluralCategory, string> categoryTexts;
            return mPluralTexts.TryGetValue(languageId, out languagePluralTexts)
                   && languagePluralTexts.TryGetValue(textId, out categoryTexts)
                   && categoryTexts.TryGetValue(category, out text);
        }

        /// <inheritdoc />
        public LanguageInfo GetLanguageInfo(LanguageId languageId)
        {
            LanguageInfo languageInfo;
            return mLanguageInfos.TryGetValue(languageId, out languageInfo) ? languageInfo : LanguageInfo.Empty;
        }

        /// <inheritdoc />
        public void PreloadLanguage(LanguageId languageId)
        {
            if (mSupportedLanguageSet.Contains(languageId))
            {
                mUnloadedLanguages.Remove(languageId);
            }
        }

        /// <inheritdoc />
        public void UnloadLanguage(LanguageId languageId)
        {
            if (mSupportedLanguageSet.Contains(languageId))
            {
                mUnloadedLanguages.Add(languageId);
            }
        }

        /// <inheritdoc />
        public bool IsLanguageLoaded(LanguageId languageId) =>
            mSupportedLanguageSet.Contains(languageId) && !mUnloadedLanguages.Contains(languageId);

        private void EnsureLanguage(LanguageId languageId)
        {
            if (mSupportedLanguageSet.Add(languageId))
            {
                mSupportedLanguages.Add(languageId);
            }
        }
    }
}
