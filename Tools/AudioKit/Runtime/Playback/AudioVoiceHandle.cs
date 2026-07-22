using System;

namespace YokiFrame
{
    /// <summary>标识一个绑定到特定后端代次的 AudioKit voice。</summary>
    public readonly struct AudioVoiceHandle : IEquatable<AudioVoiceHandle>
    {
        /// <summary>创建绑定后端代次和局部 voice ID 的不可变句柄。</summary>
        internal AudioVoiceHandle(long backendGeneration, int voiceId)
        {
            BackendGeneration = backendGeneration;
            VoiceId = voiceId;
        }

        /// <summary>获取创建 voice 时的后端代次。</summary>
        public long BackendGeneration { get; }

        /// <summary>获取后端局部 voice id。</summary>
        public int VoiceId { get; }

        /// <summary>获取当前值是否包含可用代次和 voice id。</summary>
        public bool IsValid => BackendGeneration > 0 && VoiceId > 0;

        /// <summary>比较两个 handle 是否指向同一后端代次和 voice。</summary>
        public bool Equals(AudioVoiceHandle other) =>
            BackendGeneration == other.BackendGeneration && VoiceId == other.VoiceId;

        /// <summary>比较任意对象是否为相同 voice handle。</summary>
        public override bool Equals(object obj) => obj is AudioVoiceHandle other && Equals(other);

        /// <summary>组合后端代次和 voice id 生成哈希。</summary>
        public override int GetHashCode() => unchecked((BackendGeneration.GetHashCode() * 397) ^ VoiceId);

        /// <summary>判断两个句柄是否指向相同后端代次和 voice。</summary>
        public static bool operator ==(AudioVoiceHandle left, AudioVoiceHandle right) => left.Equals(right);
        /// <summary>判断两个句柄是否指向不同后端代次或 voice。</summary>
        public static bool operator !=(AudioVoiceHandle left, AudioVoiceHandle right) => !left.Equals(right);
    }
}
