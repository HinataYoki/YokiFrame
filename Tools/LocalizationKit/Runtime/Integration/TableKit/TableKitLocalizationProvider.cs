using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>把项目生成表的查询委托标记为 TableKit 本地化后端。</summary>
    /// <remarks>生成表类型不进入框架程序集；本类型只保留稳定构造入口，查询实现全部由基类承担。</remarks>
    public sealed class TableKitLocalizationProvider : TableLocalizationProvider
    {
        /// <summary>创建 TableKit 本地化后端。</summary>
        /// <param name="supportedLanguages">生成表支持的语言。</param>
        /// <param name="textGetter">普通文本查询。</param>
        /// <param name="pluralTextGetter">复数文本查询，可为空。</param>
        /// <param name="languageInfoGetter">语言元数据查询，可为空。</param>
        /// <param name="errorHandler">查询异常回调，可为空。</param>
        public TableKitLocalizationProvider(
            IEnumerable<LanguageId> supportedLanguages,
            Func<LanguageId, int, string> textGetter,
            Func<LanguageId, int, PluralCategory, string> pluralTextGetter = null,
            Func<LanguageId, LanguageInfo> languageInfoGetter = null,
            Action<Exception> errorHandler = null)
            : base(supportedLanguages, textGetter, pluralTextGetter, languageInfoGetter, errorHandler)
        {
        }
    }
}
