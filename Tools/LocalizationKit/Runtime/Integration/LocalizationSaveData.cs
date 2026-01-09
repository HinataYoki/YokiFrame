using System;

namespace YokiFrame
{
    /// <summary>
    /// 本地化设置存档数据
    /// 用于持久化语言偏好设置
    /// </summary>
    [Serializable]
    public class LocalizationSaveData
    {
        /// <summary>
        /// 当前语言ID
        /// </summary>
        public int CurrentLanguageId;

        /// <summary>
        /// 数据版本
        /// </summary>
        public int Version;

        /// <summary>
        /// 创建默认数据
        /// </summary>
        public static LocalizationSaveData CreateDefault()
        {
            return new LocalizationSaveData
            {
                CurrentLanguageId = (int)LanguageId.ChineseSimplified,
                Version = 1
            };
        }

        /// <summary>
        /// 从当前设置创建
        /// </summary>
        public static LocalizationSaveData FromCurrentSettings()
        {
            return new LocalizationSaveData
            {
                CurrentLanguageId = (int)LocalizationKit.GetCurrentLanguage(),
                Version = 1
            };
        }

        /// <summary>
        /// 应用到 LocalizationKit
        /// </summary>
        public void Apply()
        {
            var languageId = (LanguageId)CurrentLanguageId;
            LocalizationKit.SetLanguage(languageId);
        }
    }
}
