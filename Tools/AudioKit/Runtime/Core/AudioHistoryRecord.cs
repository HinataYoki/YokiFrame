namespace YokiFrame
{
    /// <summary>
    /// 描述 AudioKit 播放、停止和音量变更历史记录。
    /// </summary>
    public sealed class AudioHistoryRecord
    {
        /// <summary>
        /// 历史事件类型。
        /// </summary>
        public string EventType;

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
        /// 事件发生时的音量。
        /// </summary>
        public float Volume;

        /// <summary>
        /// 事件发生时的音调。
        /// </summary>
        public float Pitch;

        /// <summary>
        /// 淡出时长。
        /// </summary>
        public float FadeOutDuration;

        /// <summary>
        /// 是否循环播放。
        /// </summary>
        public bool Loop;

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

        /// <summary>
        /// 事件发生的 UTC 时间。
        /// </summary>
        public string TimestampUtc;
    }
}
