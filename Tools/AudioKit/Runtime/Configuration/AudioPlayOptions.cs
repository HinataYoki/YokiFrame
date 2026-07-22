using System.Numerics;

namespace YokiFrame
{
    /// <summary>保存不依赖 Unity 或 Godot 类型的单次播放参数。</summary>
    public struct AudioPlayOptions
    {
        /// <summary>逻辑总线名称。</summary>
        public string Bus;
        /// <summary>是否循环播放。</summary>
        public bool Loop;
        /// <summary>单次播放基础音量，范围为零到一。</summary>
        public float Volume;
        /// <summary>单次播放音高倍率。</summary>
        public float Pitch;
        /// <summary>线性淡入秒数。</summary>
        public float FadeInDuration;
        /// <summary>默认淡出秒数，供后端或控制策略使用。</summary>
        public float FadeOutDuration;
        /// <summary>是否启用三维空间播放。</summary>
        public bool Is3D;
        /// <summary>不跟随目标时的固定世界位置。</summary>
        public Vector3 Position;
        /// <summary>可选宿主无关跟随目标。</summary>
        public IAudioFollowTarget FollowTarget;
        /// <summary>三维衰减最小距离。</summary>
        public float MinDistance;
        /// <summary>三维衰减最大距离。</summary>
        public float MaxDistance;
        /// <summary>三维距离衰减模式。</summary>
        public AudioRolloffMode RolloffMode;

        /// <summary>获取可直接修改后传入播放 API 的默认参数。</summary>
        public static AudioPlayOptions Default
        {
            get
            {
                return new AudioPlayOptions
                {
                    Bus = AudioBus.Sfx,
                    Volume = 1f,
                    Pitch = 1f,
                    MinDistance = 1f,
                    MaxDistance = 500f,
                    RolloffMode = AudioRolloffMode.Logarithmic
                };
            }
        }
    }
}
