using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 通过委托接入 TableKit/Luban 生成的本地化配置表。
    /// </summary>
    public sealed class TableKitLocalizationProvider : ILocalizationProvider
    {
        private readonly Func<LanguageId, int, string> mTextGetter;
        private readonly Func<LanguageId, int, PluralCategory, string> mPluralTextGetter;
        private readonly Func<LanguageId, LanguageInfo> mLanguageInfoGetter;
        private readonly List<LanguageId> mSupportedLanguages;
        private readonly HashSet<LanguageId> mSupportedLanguageSet = new();
        private readonly HashSet<LanguageId> mLoadedLanguages = new();

        /// <summary>
        /// 创建 TableKit 本地化数据提供器。
        /// </summary>
        /// <param name="supportedLanguages">配置表支持的语言列表。</param>
        /// <param name="textGetter">普通文本查询委托。</param>
        /// <param name="pluralTextGetter">复数文本查询委托；为空时回退到普通文本。</param>
        /// <param name="languageInfoGetter">语言显示信息查询委托；为空时返回默认语言信息。</param>
        public TableKitLocalizationProvider(
            IEnumerable<LanguageId> supportedLanguages,
            Func<LanguageId, int, string> textGetter,
            Func<LanguageId, int, PluralCategory, string> pluralTextGetter = null,
            Func<LanguageId, LanguageInfo> languageInfoGetter = null)
        {
            if (supportedLanguages == null)
            {
                throw new ArgumentNullException(nameof(supportedLanguages));
            }

            if (textGetter == null)
            {
                throw new ArgumentNullException(nameof(textGetter));
            }

            mSupportedLanguages = new List<LanguageId>();
            foreach (LanguageId languageId in supportedLanguages)
            {
                if (mSupportedLanguageSet.Add(languageId))
                {
                    mSupportedLanguages.Add(languageId);
                    mLoadedLanguages.Add(languageId);
                }
            }

            mTextGetter = textGetter;
            mPluralTextGetter = pluralTextGetter;
            mLanguageInfoGetter = languageInfoGetter;
        }

        /// <inheritdoc />
        public IReadOnlyList<LanguageId> GetSupportedLanguages()
        {
            return mSupportedLanguages;
        }

        /// <inheritdoc />
        public bool TryGetText(LanguageId languageId, int textId, out string text)
        {
            text = null;
            if (!IsLanguageLoaded(languageId))
            {
                return false;
            }

            try
            {
                text = mTextGetter(languageId, textId);
                return text != null;
            }
            catch (Exception e)
            {
                KitLogger.Warning("[LocalizationKit] TableKit 获取文本失败: " + e.Message);
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryGetPluralText(LanguageId languageId, int textId, PluralCategory category, out string text)
        {
            text = null;
            if (!IsLanguageLoaded(languageId))
            {
                return false;
            }

            if (mPluralTextGetter == null)
            {
                return TryGetText(languageId, textId, out text);
            }

            try
            {
                text = mPluralTextGetter(languageId, textId, category);
                if (text == null && category != PluralCategory.Other)
                {
                    text = mPluralTextGetter(languageId, textId, PluralCategory.Other);
                }

                return text != null;
            }
            catch (Exception e)
            {
                KitLogger.Warning("[LocalizationKit] TableKit 获取复数文本失败: " + e.Message);
                return false;
            }
        }

        /// <inheritdoc />
        public LanguageInfo GetLanguageInfo(LanguageId languageId)
        {
            if (mLanguageInfoGetter == null)
            {
                return mSupportedLanguageSet.Contains(languageId)
                    ? new LanguageInfo(languageId, 0, 0, 0)
                    : LanguageInfo.Empty;
            }

            try
            {
                return mLanguageInfoGetter(languageId);
            }
            catch (Exception e)
            {
                KitLogger.Warning("[LocalizationKit] TableKit 获取语言信息失败: " + e.Message);
                return LanguageInfo.Empty;
            }
        }

        /// <inheritdoc />
        public void PreloadLanguage(LanguageId languageId)
        {
            if (mSupportedLanguageSet.Contains(languageId))
            {
                mLoadedLanguages.Add(languageId);
            }
        }

        /// <inheritdoc />
        public void UnloadLanguage(LanguageId languageId)
        {
            if (mSupportedLanguageSet.Contains(languageId))
            {
                mLoadedLanguages.Remove(languageId);
            }
        }

        /// <inheritdoc />
        public bool IsLanguageLoaded(LanguageId languageId)
        {
            return mSupportedLanguageSet.Contains(languageId) && mLoadedLanguages.Contains(languageId);
        }
    }
}
