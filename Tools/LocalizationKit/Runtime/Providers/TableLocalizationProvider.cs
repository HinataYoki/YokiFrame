using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace YokiFrame
{
    /// <summary>通过委托访问任意配置表的通用本地化 Provider。</summary>
    /// <remarks>TableKit/Luban 生成代码通过 Integration 层把查询委托接入此 Provider。</remarks>
    public sealed class TableLocalizationProvider : ILocalizationProvider
    {
        private readonly Func<LanguageId, int, string> mTextGetter;
        private readonly Func<LanguageId, int, PluralCategory, string> mPluralTextGetter;
        private readonly Func<LanguageId, LanguageInfo> mLanguageInfoGetter;
        private readonly Action<Exception> mErrorHandler;
        private readonly List<LanguageId> mSupportedLanguages = new List<LanguageId>();
        private readonly ReadOnlyCollection<LanguageId> mSupportedLanguageView;
        private readonly HashSet<LanguageId> mSupportedLanguageSet = new HashSet<LanguageId>();
        private readonly HashSet<LanguageId> mLoadedLanguages = new HashSet<LanguageId>();

        /// <summary>创建通用表 Provider。</summary>
        /// <param name="supportedLanguages">表中存在的语言。</param>
        /// <param name="textGetter">普通文本查询函数。</param>
        /// <param name="pluralTextGetter">复数文本查询函数，可为空。</param>
        /// <param name="languageInfoGetter">语言元数据查询函数，可为空。</param>
        /// <param name="errorHandler">查询异常回调，可为空。</param>
        public TableLocalizationProvider(
            IEnumerable<LanguageId> supportedLanguages,
            Func<LanguageId, int, string> textGetter,
            Func<LanguageId, int, PluralCategory, string> pluralTextGetter = null,
            Func<LanguageId, LanguageInfo> languageInfoGetter = null,
            Action<Exception> errorHandler = null)
        {
            if (supportedLanguages == null) throw new ArgumentNullException(nameof(supportedLanguages));
            if (textGetter == null) throw new ArgumentNullException(nameof(textGetter));

            foreach (LanguageId languageId in supportedLanguages)
            {
                if (mSupportedLanguageSet.Add(languageId))
                {
                    mSupportedLanguages.Add(languageId);
                    mLoadedLanguages.Add(languageId);
                }
            }

            mSupportedLanguageView = mSupportedLanguages.AsReadOnly();
            mTextGetter = textGetter;
            mPluralTextGetter = pluralTextGetter;
            mLanguageInfoGetter = languageInfoGetter;
            mErrorHandler = errorHandler;
        }

        /// <inheritdoc />
        public IReadOnlyList<LanguageId> GetSupportedLanguages() => mSupportedLanguageView;

        /// <inheritdoc />
        public bool TryGetText(LanguageId languageId, int textId, out string text)
        {
            text = null;
            if (!IsLanguageLoaded(languageId)) return false;

            try
            {
                text = mTextGetter(languageId, textId);
                return text != null;
            }
            catch (Exception exception)
            {
                ReportError(exception);
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryGetPluralText(LanguageId languageId, int textId, PluralCategory category, out string text)
        {
            text = null;
            if (!IsLanguageLoaded(languageId)) return false;
            if (mPluralTextGetter == null) return TryGetText(languageId, textId, out text);

            try
            {
                text = mPluralTextGetter(languageId, textId, category);
                if (text == null && category != PluralCategory.Other)
                    text = mPluralTextGetter(languageId, textId, PluralCategory.Other);
                return text != null;
            }
            catch (Exception exception)
            {
                ReportError(exception);
                return false;
            }
        }

        /// <inheritdoc />
        public LanguageInfo GetLanguageInfo(LanguageId languageId)
        {
            if (mLanguageInfoGetter == null)
                return mSupportedLanguageSet.Contains(languageId) ? new LanguageInfo(languageId, 0, 0, 0) : LanguageInfo.Empty;

            try
            {
                return mLanguageInfoGetter(languageId);
            }
            catch (Exception exception)
            {
                ReportError(exception);
                return LanguageInfo.Empty;
            }
        }

        /// <inheritdoc />
        public void PreloadLanguage(LanguageId languageId)
        {
            if (mSupportedLanguageSet.Contains(languageId)) mLoadedLanguages.Add(languageId);
        }

        /// <inheritdoc />
        public void UnloadLanguage(LanguageId languageId)
        {
            if (mSupportedLanguageSet.Contains(languageId)) mLoadedLanguages.Remove(languageId);
        }

        /// <inheritdoc />
        public bool IsLanguageLoaded(LanguageId languageId) =>
            mSupportedLanguageSet.Contains(languageId) && mLoadedLanguages.Contains(languageId);

        /// <summary>把表委托异常交给调用方诊断回调，避免 Runtime 依赖具体日志系统。</summary>
        private void ReportError(Exception exception)
        {
            if (mErrorHandler != null) mErrorHandler(exception);
        }
    }
}
