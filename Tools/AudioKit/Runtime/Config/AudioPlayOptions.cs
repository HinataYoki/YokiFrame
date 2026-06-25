namespace YokiFrame
{
    /// <summary>
    /// 播放参数值对象。这里不能暴露 Unity AudioSource、Transform 或 Godot Node。
    /// </summary>
    public struct AudioPlayOptions
    {
        /// <summary>
        /// 播放使用的音频总线。
        /// </summary>
        public string Bus;

        /// <summary>
        /// 是否循环播放。
        /// </summary>
        public bool Loop;

        /// <summary>
        /// 播放音量。
        /// </summary>
        public float Volume;

        /// <summary>
        /// 播放音调。
        /// </summary>
        public float Pitch;

        /// <summary>
        /// 淡入时长，单位秒。
        /// </summary>
        public float FadeInDuration;

        /// <summary>
        /// 淡出时长，单位秒。
        /// </summary>
        public float FadeOutDuration;

        /// <summary>
        /// 是否按 3D 音频语义播放。
        /// </summary>
        public bool Is3D;

        /// <summary>
        /// 3D 音频播放位置。
        /// </summary>
        public YokiVector3 Position;

        /// <summary>
        /// 3D 音频跟随目标。
        /// </summary>
        public IEngineObject FollowTarget;

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
        /// 获取默认播放参数。
        /// </summary>
        public static AudioPlayOptions Default
        {
            get
            {
                return new()
                {
                    Bus = AudioBus.Sfx,
                    Loop = false,
                    Volume = 1f,
                    Pitch = 1f,
                    FadeInDuration = 0f,
                    FadeOutDuration = 0f,
                    Is3D = false,
                    Position = YokiVector3.Zero,
                    FollowTarget = null,
                    MinDistance = 1f,
                    MaxDistance = 500f,
                    RolloffMode = AudioRolloffMode.Logarithmic
                };
            }
        }
    }
}
