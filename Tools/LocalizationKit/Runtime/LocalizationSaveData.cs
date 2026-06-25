using System;

namespace YokiFrame
{
    /// <summary>
    /// 保存 LocalizationKit 当前语言偏好的 SaveKit 模块。
    /// </summary>
    [Serializable]
    public sealed class LocalizationSaveData
    {
        /// <summary>
        /// 当前数据版本。
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// 创建语言偏好保存数据。
        /// </summary>
        public LocalizationSaveData()
            : this(LanguageId.ChineseSimplified, CurrentVersion)
        {
        }

        /// <summary>
        /// 创建语言偏好保存数据。
        /// </summary>
        /// <param name="language">保存的语言。</param>
        /// <param name="version">保存数据版本。</param>
        public LocalizationSaveData(LanguageId language, int version)
        {
            Language = language;
            Version = version;
        }

        /// <summary>
        /// 保存的语言整数值，使用字段以兼容 Unity/项目常见序列化器。
        /// </summary>
        public int CurrentLanguageId;

        /// <summary>
        /// 保存数据版本。
        /// </summary>
        public int Version;

        /// <summary>
        /// 保存的语言。
        /// </summary>
        public LanguageId Language
        {
            get { return (LanguageId)CurrentLanguageId; }
            set { CurrentLanguageId = (int)value; }
        }

        /// <summary>
        /// 创建默认语言偏好。
        /// </summary>
        /// <returns>默认语言偏好保存数据。</returns>
        public static LocalizationSaveData CreateDefault()
        {
            return new LocalizationSaveData(LanguageId.ChineseSimplified, CurrentVersion);
        }

        /// <summary>
        /// 从 LocalizationKit 当前语言创建保存数据。
        /// </summary>
        /// <returns>当前语言偏好保存数据。</returns>
        public static LocalizationSaveData FromCurrentSettings()
        {
            return new LocalizationSaveData(LocalizationKit.GetCurrentLanguage(), CurrentVersion);
        }

        /// <summary>
        /// 应用保存的语言偏好。
        /// </summary>
        /// <returns>语言切换成功时返回 true。</returns>
        public bool Apply()
        {
            return LocalizationKit.SetLanguage(Language);
        }
    }
}
