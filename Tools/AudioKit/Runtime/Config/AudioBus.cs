namespace YokiFrame
{
    /// <summary>
    /// AudioKit 默认音频总线名称。
    /// </summary>
    public static class AudioBus
    {
        /// <summary>
        /// 主总线。
        /// </summary>
        public const string MASTER = "Master";

        /// <summary>
        /// 音乐总线。
        /// </summary>
        public const string MUSIC = "Music";

        /// <summary>
        /// 音效总线。
        /// </summary>
        public const string SFX = "Sfx";

        /// <summary>
        /// 语音总线。
        /// </summary>
        public const string VOICE = "Voice";

        /// <summary>
        /// 环境声总线。
        /// </summary>
        public const string AMBIENCE = "Ambience";

        /// <summary>
        /// UI 音频总线。
        /// </summary>
        public const string UI = "UI";

        /// <summary>
        /// 主总线。
        /// </summary>
        public static string Master => MASTER;

        /// <summary>
        /// 音乐总线。
        /// </summary>
        public static string Music => MUSIC;

        /// <summary>
        /// 音效总线。
        /// </summary>
        public static string Sfx => SFX;

        /// <summary>
        /// 语音总线。
        /// </summary>
        public static string Voice => VOICE;

        /// <summary>
        /// 环境声总线。
        /// </summary>
        public static string Ambience => AMBIENCE;

        /// <summary>
        /// UI 音频总线。
        /// </summary>
        public static string Ui => UI;
    }
}
