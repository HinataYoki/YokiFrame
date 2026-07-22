#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Numerics;

namespace YokiFrame
{
    /// <summary>描述一个 active voice 的有界工具诊断状态。</summary>
    public sealed class AudioVoiceSnapshot
    {
        /// <summary>创建 voice 时的后端代次。</summary>
        public long BackendGeneration;
        /// <summary>后端局部 voice ID。</summary>
        public int VoiceId;
        /// <summary>音频资源或事件路径。</summary>
        public string Path;
        /// <summary>逻辑总线名称。</summary>
        public string Bus;
        /// <summary>实际后端展示名称。</summary>
        public string BackendName;
        /// <summary>是否循环播放。</summary>
        public bool Loop;
        /// <summary>后端是否仍报告播放中。</summary>
        public bool IsPlaying;
        /// <summary>voice 是否暂停。</summary>
        public bool IsPaused;
        /// <summary>当前线性音量。</summary>
        public float Volume;
        /// <summary>当前音高倍率。</summary>
        public float Pitch;
        /// <summary>可用时的总时长秒数。</summary>
        public float Duration;
        /// <summary>可用时的已播放秒数。</summary>
        public float Elapsed;
        /// <summary>是否为三维 voice。</summary>
        public bool Is3D;
        /// <summary>当前世界位置。</summary>
        public Vector3 Position;
        /// <summary>可选跟随目标展示名称。</summary>
        public string FollowTargetName;
        /// <summary>三维衰减最小距离。</summary>
        public float MinDistance;
        /// <summary>三维衰减最大距离。</summary>
        public float MaxDistance;
        /// <summary>三维距离衰减模式。</summary>
        public AudioRolloffMode RolloffMode;
    }
}
#endif
