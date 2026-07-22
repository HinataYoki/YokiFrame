namespace YokiFrame
{
    /// <summary>定义 AudioKit 内置逻辑总线名称；项目仍可使用任意非空自定义总线。</summary>
    public static class AudioBus
    {
        /// <summary>Master 总线稳定名称。</summary>
        public const string Master = "Master";
        /// <summary>Music 总线稳定名称。</summary>
        public const string Music = "Music";
        /// <summary>Sfx 总线稳定名称。</summary>
        public const string Sfx = "Sfx";
        /// <summary>Voice 总线稳定名称。</summary>
        public const string Voice = "Voice";
        /// <summary>Ambience 总线稳定名称。</summary>
        public const string Ambience = "Ambience";
        /// <summary>UI 总线稳定名称。</summary>
        public const string UI = "UI";

        /// <summary>兼容旧 MASTER 常量。</summary>
        public const string MASTER = Master;
        /// <summary>兼容旧 MUSIC 常量。</summary>
        public const string MUSIC = Music;
        /// <summary>兼容旧 SFX 常量。</summary>
        public const string SFX = Sfx;
        /// <summary>兼容旧 VOICE 常量。</summary>
        public const string VOICE = Voice;
        /// <summary>兼容旧 AMBIENCE 常量。</summary>
        public const string AMBIENCE = Ambience;
        /// <summary>兼容旧 Ui 属性拼写。</summary>
        public static string Ui => UI;
    }
}
