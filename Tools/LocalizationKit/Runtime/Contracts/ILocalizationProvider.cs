using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>定义语言文本、复数文本和语言元数据的数据来源。</summary>
    public interface ILocalizationProvider
    {
        /// <summary>获取 Provider 支持的语言，只读列表。</summary>
        IReadOnlyList<LanguageId> GetSupportedLanguages();
        /// <summary>尝试读取普通文本。</summary>
        bool TryGetText(LanguageId languageId, int textId, out string text);
        /// <summary>尝试读取指定复数分类文本；缺失分类时可回退到 Other。</summary>
        bool TryGetPluralText(LanguageId languageId, int textId, PluralCategory category, out string text);
        /// <summary>获取语言显示信息。</summary>
        LanguageInfo GetLanguageInfo(LanguageId languageId);
        /// <summary>预加载语言数据。</summary>
        void PreloadLanguage(LanguageId languageId);
        /// <summary>卸载语言数据。</summary>
        void UnloadLanguage(LanguageId languageId);
        /// <summary>判断语言数据是否已加载。</summary>
        bool IsLanguageLoaded(LanguageId languageId);
    }
}
