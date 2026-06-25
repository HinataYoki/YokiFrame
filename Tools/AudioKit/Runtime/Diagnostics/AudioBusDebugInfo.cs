namespace YokiFrame
{
    /// <summary>
    /// 描述单个音频总线的调试信息。
    /// </summary>
    public sealed class AudioBusDebugInfo
    {
        /// <summary>
        /// 总线名称。
        /// </summary>
        public string Name;

        /// <summary>
        /// 配置音量。
        /// </summary>
        public float Volume;

        /// <summary>
        /// 最终生效音量。
        /// </summary>
        public float EffectiveVolume;

        /// <summary>
        /// 当前是否静音。
        /// </summary>
        public bool Muted;

        /// <summary>
        /// 是否为主总线。
        /// </summary>
        public bool IsMaster;

        /// <summary>
        /// 是否为默认总线。
        /// </summary>
        public bool IsDefault;

        /// <summary>
        /// 当前活跃 Voice 数量。
        /// </summary>
        public int ActiveVoiceCount;
    }
}
