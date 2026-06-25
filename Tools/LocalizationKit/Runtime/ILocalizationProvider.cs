using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 定义本地化文本、复数文本和语言元数据的数据提供器。
    /// </summary>
    public interface ILocalizationProvider
    {
        /// <summary>
        /// 获取当前支持的语言列表。
        /// </summary>
        /// <returns>支持的语言列表。</returns>
        IReadOnlyList<LanguageId> GetSupportedLanguages();

        /// <summary>
        /// 尝试获取普通文本。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        /// <param name="textId">文本编号。</param>
        /// <param name="text">成功时输出文本。</param>
        /// <returns>找到文本时返回 true。</returns>
        bool TryGetText(LanguageId languageId, int textId, out string text);

        /// <summary>
        /// 尝试获取复数文本。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        /// <param name="textId">文本编号。</param>
        /// <param name="category">复数分类。</param>
        /// <param name="text">成功时输出文本。</param>
        /// <returns>找到文本时返回 true。</returns>
        bool TryGetPluralText(LanguageId languageId, int textId, PluralCategory category, out string text);

        /// <summary>
        /// 获取语言显示信息。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        /// <returns>语言显示信息。</returns>
        LanguageInfo GetLanguageInfo(LanguageId languageId);

        /// <summary>
        /// 预加载指定语言。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        void PreloadLanguage(LanguageId languageId);

        /// <summary>
        /// 卸载指定语言。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        void UnloadLanguage(LanguageId languageId);

        /// <summary>
        /// 判断指定语言是否已加载。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        /// <returns>已加载时返回 true。</returns>
        bool IsLanguageLoaded(LanguageId languageId);
    }
}
