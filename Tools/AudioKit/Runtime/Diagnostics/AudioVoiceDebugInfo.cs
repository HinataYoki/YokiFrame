namespace YokiFrame
{
    /// <summary>
    /// 描述当前活跃音频 Voice 的调试信息。
    /// </summary>
    public sealed class AudioVoiceDebugInfo
    {
        /// <summary>
        /// Voice 唯一编号。
        /// </summary>
        public int VoiceId;

        /// <summary>
        /// 音频资源路径。
        /// </summary>
        public string Path;

        /// <summary>
        /// 音频剪辑名称。
        /// </summary>
        public string ClipName;

        /// <summary>
        /// 音频总线。
        /// </summary>
        public string Bus;

        /// <summary>
        /// 后端名称。
        /// </summary>
        public string BackendName;

        /// <summary>
        /// 是否循环播放。
        /// </summary>
        public bool Loop;

        /// <summary>
        /// 是否正在播放。
        /// </summary>
        public bool IsPlaying;

        /// <summary>
        /// 当前音量。
        /// </summary>
        public float Volume;

        /// <summary>
        /// 当前音调。
        /// </summary>
        public float Pitch;

        /// <summary>
        /// 淡出时长。
        /// </summary>
        public float FadeOutDuration;

        /// <summary>
        /// 开始播放的宿主时间。
        /// </summary>
        public float StartedAt;

        /// <summary>
        /// 音频总时长。
        /// </summary>
        public float Duration;

        /// <summary>
        /// 已播放时长。
        /// </summary>
        public float Elapsed;

        /// <summary>
        /// 是否为 3D 音频。
        /// </summary>
        public bool Is3D;

        /// <summary>
        /// 3D 音频位置。
        /// </summary>
        public YokiVector3 Position;

        /// <summary>
        /// 是否存在跟随目标。
        /// </summary>
        public bool HasFollowTarget;

        /// <summary>
        /// 跟随目标名称。
        /// </summary>
        public string FollowTargetName;

        /// <summary>
        /// 3D 音频最小距离。
        /// </summary>
        public float MinDistance;

        /// <summary>
        /// 3D 音频最大距离。
        /// </summary>
        public float MaxDistance;

        /// <summary>
        /// 3D 音频距离衰减模式。
        /// </summary>
        public AudioRolloffMode RolloffMode;
    }
}
