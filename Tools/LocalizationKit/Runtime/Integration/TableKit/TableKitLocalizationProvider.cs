using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>把 Luban/TableKit 生成表的查询委托明确标记为 LocalizationKit 后端。</summary>
    /// <remarks>生成表类型不进入框架程序集；项目生成代码只需把自身查询方法传入构造函数。</remarks>
    public sealed class TableKitLocalizationProvider : ILocalizationProvider
    {
        private readonly TableLocalizationProvider mInner;

        /// <summary>创建 TableKit 本地化后端。</summary>
        /// <param name="supportedLanguages">生成表支持的语言。</param>
        /// <param name="textGetter">普通文本查询。</param>
        /// <param name="pluralTextGetter">复数文本查询，可为空。</param>
        /// <param name="languageInfoGetter">语言元数据查询，可为空。</param>
        /// <param name="errorHandler">查询异常回调，可为空。</param>
        public TableKitLocalizationProvider(IEnumerable<LanguageId> supportedLanguages,
            Func<LanguageId, int, string> textGetter,
            Func<LanguageId, int, PluralCategory, string> pluralTextGetter = null,
            Func<LanguageId, LanguageInfo> languageInfoGetter = null,
            Action<Exception> errorHandler = null)
        {
            mInner = new TableLocalizationProvider(supportedLanguages, textGetter, pluralTextGetter, languageInfoGetter, errorHandler);
        }

        /// <inheritdoc />
        public IReadOnlyList<LanguageId> GetSupportedLanguages() => mInner.GetSupportedLanguages();
        /// <inheritdoc />
        public bool TryGetText(LanguageId languageId, int textId, out string text) => mInner.TryGetText(languageId, textId, out text);
        /// <inheritdoc />
        public bool TryGetPluralText(LanguageId languageId, int textId, PluralCategory category, out string text) => mInner.TryGetPluralText(languageId, textId, category, out text);
        /// <inheritdoc />
        public LanguageInfo GetLanguageInfo(LanguageId languageId) => mInner.GetLanguageInfo(languageId);
        /// <inheritdoc />
        public void PreloadLanguage(LanguageId languageId) => mInner.PreloadLanguage(languageId);
        /// <inheritdoc />
        public void UnloadLanguage(LanguageId languageId) => mInner.UnloadLanguage(languageId);
        /// <inheritdoc />
        public bool IsLanguageLoaded(LanguageId languageId) => mInner.IsLanguageLoaded(languageId);
    }
}
