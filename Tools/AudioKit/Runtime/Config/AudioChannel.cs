namespace YokiFrame
{
    /// <summary>
    /// AudioKit 内置通道枚举。
    /// </summary>
    public enum AudioChannel
    {
        /// <summary>
        /// 音乐通道。
        /// </summary>
        Music = 0,

        /// <summary>
        /// 背景音乐通道，等同于 Music。
        /// </summary>
        Bgm = Music,

        /// <summary>
        /// 音效通道。
        /// </summary>
        Sfx = 1,

        /// <summary>
        /// 语音通道。
        /// </summary>
        Voice = 2,

        /// <summary>
        /// 环境声通道。
        /// </summary>
        Ambience = 3,

        /// <summary>
        /// 环境声通道别名。
        /// </summary>
        Ambient = Ambience,

        /// <summary>
        /// UI 音频通道。
        /// </summary>
        UI = 4
    }
}
