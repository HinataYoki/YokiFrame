using System;

namespace YokiFrame
{
    /// <summary>保存当前语言偏好的纯数据模块，具体序列化由 SaveKit 负责。</summary>
    [Serializable]
    public sealed class LocalizationSaveData
    {
        /// <summary>当前保存数据版本。</summary>
        public const int CurrentVersion = 1;

        /// <summary>创建默认语言保存数据。</summary>
        public LocalizationSaveData() : this(LanguageId.ChineseSimplified, CurrentVersion)
        {
        }

        /// <summary>创建指定语言保存数据。</summary>
        public LocalizationSaveData(LanguageId language, int version)
        {
            CurrentLanguageId = (int)language;
            Version = version;
        }

        /// <summary>语言整数值，保持 Unity 常见序列化器兼容。</summary>
        public int CurrentLanguageId;
        /// <summary>保存数据版本。</summary>
        public int Version;

        /// <summary>获取或设置保存的语言。</summary>
        public LanguageId Language
        {
            get => (LanguageId)CurrentLanguageId;
            set => CurrentLanguageId = (int)value;
        }

        /// <summary>创建默认保存数据。</summary>
        public static LocalizationSaveData CreateDefault() =>
            new LocalizationSaveData(LanguageId.ChineseSimplified, CurrentVersion);

        /// <summary>从当前 LocalizationKit 状态创建保存数据。</summary>
        public static LocalizationSaveData FromCurrentSettings() =>
            new LocalizationSaveData(LocalizationKit.GetCurrentLanguage(), CurrentVersion);

        /// <summary>应用保存的语言；目标语言不受 Provider 支持时返回 false。</summary>
        public bool Apply() => LocalizationKit.SetLanguage(Language);
    }
}
