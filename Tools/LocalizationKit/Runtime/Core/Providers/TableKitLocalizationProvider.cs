#if YOKIFRAME_LUBAN_SUPPORT
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// TableKit 配置表本地化数据提供者
    /// 集成 Luban 配置表系统，从配置表加载本地化数据
    /// </summary>
    public class TableKitLocalizationProvider : ILocalizationProvider
    {
        // 文本获取委托：(languageId, textId) -> text
        private readonly Func<LanguageId, int, string> mTextGetter;
        
        // 复数文本获取委托：(languageId, textId, category) -> text
        private readonly Func<LanguageId, int, PluralCategory, string> mPluralTextGetter;
        
        // 语言信息获取委托
        private readonly Func<LanguageId, LanguageInfo> mLanguageInfoGetter;
        
        // 支持的语言列表
        private readonly List<LanguageId> mSupportedLanguages;
        
        // 已加载的语言
        private readonly HashSet<LanguageId> mLoadedLanguages = new();

        /// <summary>
        /// 创建 TableKit 本地化提供者
        /// </summary>
        /// <param name="supportedLanguages">支持的语言列表</param>
        /// <param name="textGetter">文本获取委托</param>
        /// <param name="pluralTextGetter">复数文本获取委托（可选）</param>
        /// <param name="languageInfoGetter">语言信息获取委托（可选）</param>
        public TableKitLocalizationProvider(
            IEnumerable<LanguageId> supportedLanguages,
            Func<LanguageId, int, string> textGetter,
            Func<LanguageId, int, PluralCategory, string> pluralTextGetter = null,
            Func<LanguageId, LanguageInfo> languageInfoGetter = null)
        {
            mSupportedLanguages = new List<LanguageId>(supportedLanguages);
            mTextGetter = textGetter ?? throw new ArgumentNullException(nameof(textGetter));
            mPluralTextGetter = pluralTextGetter;
            mLanguageInfoGetter = languageInfoGetter;

            // 默认所有语言都已加载（配置表在启动时加载）
            foreach (var lang in mSupportedLanguages)
            {
                mLoadedLanguages.Add(lang);
            }
        }

        public IReadOnlyList<LanguageId> GetSupportedLanguages()
        {
            return mSupportedLanguages;
        }

        public bool TryGetText(LanguageId languageId, int textId, out string text)
        {
            text = null;

            if (mTextGetter == null)
                return false;

            try
            {
                text = mTextGetter(languageId, textId);
                return text != null;
            }
            catch (Exception e)
            {
                KitLogger.Warning($"[LocalizationKit] TableKit 获取文本失败: {e.Message}");
                return false;
            }
        }

        public bool TryGetPluralText(LanguageId languageId, int textId, PluralCategory category, out string text)
        {
            text = null;

            if (mPluralTextGetter == null)
            {
                // 如果没有复数获取器，fallback 到普通文本
                return TryGetText(languageId, textId, out text);
            }

            try
            {
                text = mPluralTextGetter(languageId, textId, category);
                
                // 如果指定类别没有，fallback 到 Other
                if (text == null && category != PluralCategory.Other)
                {
                    text = mPluralTextGetter(languageId, textId, PluralCategory.Other);
                }

                return text != null;
            }
            catch (Exception e)
            {
                KitLogger.Warning($"[LocalizationKit] TableKit 获取复数文本失败: {e.Message}");
                return false;
            }
        }

        public LanguageInfo GetLanguageInfo(LanguageId languageId)
        {
            if (mLanguageInfoGetter == null)
            {
                // 返回默认语言信息
                return new LanguageInfo(languageId, 0, 0, 0);
            }

            try
            {
                return mLanguageInfoGetter(languageId);
            }
            catch (Exception e)
            {
                KitLogger.Warning($"[LocalizationKit] TableKit 获取语言信息失败: {e.Message}");
                return LanguageInfo.Empty;
            }
        }

        public void PreloadLanguage(LanguageId languageId)
        {
            // TableKit 模式下，配置表在启动时已加载
            mLoadedLanguages.Add(languageId);
        }

        public void UnloadLanguage(LanguageId languageId)
        {
            // TableKit 模式下，配置表由 TableKit 管理，这里只标记状态
            mLoadedLanguages.Remove(languageId);
        }

        public bool IsLanguageLoaded(LanguageId languageId)
        {
            return mLoadedLanguages.Contains(languageId);
        }
    }
}
#endif
