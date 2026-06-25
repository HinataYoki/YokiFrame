namespace YokiFrame
{
    /// <summary>
    /// 描述 AudioKit 当前统计信息。
    /// </summary>
    public sealed class AudioKitStats
    {
        /// <summary>
        /// 当前后端名称。
        /// </summary>
        public string BackendName;

        /// <summary>
        /// 当前活跃 Voice 数量。
        /// </summary>
        public int ActiveVoiceCount;

        /// <summary>
        /// 当前历史记录数量。
        /// </summary>
        public int HistoryCount;

        /// <summary>
        /// 主总线音量。
        /// </summary>
        public float MasterVolume;

        /// <summary>
        /// 音乐总线音量。
        /// </summary>
        public float MusicVolume;

        /// <summary>
        /// 音效总线音量。
        /// </summary>
        public float SfxVolume;

        /// <summary>
        /// 语音总线音量。
        /// </summary>
        public float VoiceVolume;

        /// <summary>
        /// 环境声总线音量。
        /// </summary>
        public float AmbienceVolume;

        /// <summary>
        /// UI 音频总线音量。
        /// </summary>
        public float UiVolume;
    }
}
