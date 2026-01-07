using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 本地化数据提供者接口
    /// 定义文本数据的加载和查询方法，支持多种数据源实现
    /// </summary>
    public interface ILocalizationProvider
    {
        /// <summary>
        /// 获取支持的语言列表
        /// </summary>
        IReadOnlyList<LanguageId> GetSupportedLanguages();

        /// <summary>
        /// 尝试获取指定语言的文本
        /// </summary>
        /// <param name="languageId">语言标识符</param>
        /// <param name="textId">文本标识符</param>
        /// <param name="text">输出的文本内容</param>
        /// <returns>是否成功获取</returns>
        bool TryGetText(LanguageId languageId, int textId, out string text);

        /// <summary>
        /// 尝试获取复数形式文本
        /// </summary>
        /// <param name="languageId">语言标识符</param>
        /// <param name="textId">文本标识符</param>
        /// <param name="category">复数类别</param>
        /// <param name="text">输出的文本内容</param>
        /// <returns>是否成功获取</returns>
        bool TryGetPluralText(LanguageId languageId, int textId, PluralCategory category, out string text);

        /// <summary>
        /// 获取语言信息
        /// </summary>
        /// <param name="languageId">语言标识符</param>
        /// <returns>语言信息，如果不存在返回 LanguageInfo.Empty</returns>
        LanguageInfo GetLanguageInfo(LanguageId languageId);

        /// <summary>
        /// 预加载指定语言的数据
        /// </summary>
        /// <param name="languageId">语言标识符</param>
        void PreloadLanguage(LanguageId languageId);

        /// <summary>
        /// 卸载指定语言的数据
        /// </summary>
        /// <param name="languageId">语言标识符</param>
        void UnloadLanguage(LanguageId languageId);

        /// <summary>
        /// 检查指定语言是否已加载
        /// </summary>
        /// <param name="languageId">语言标识符</param>
        /// <returns>是否已加载</returns>
        bool IsLanguageLoaded(LanguageId languageId);
    }
}
