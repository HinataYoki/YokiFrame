#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>保存 AudioKit 最近一次播放或控制事件的有界诊断记录。</summary>
    public sealed class AudioHistoryEntry
    {
        /// <summary>当前会话单调历史序号。</summary>
        public long Sequence;
        /// <summary>播放或控制事件类型。</summary>
        public string EventType;
        /// <summary>关联 voice 的后端代次。</summary>
        public long BackendGeneration;
        /// <summary>关联后端局部 voice ID。</summary>
        public int VoiceId;
        /// <summary>关联音频路径。</summary>
        public string Path;
        /// <summary>关联逻辑总线。</summary>
        public string Bus;
        /// <summary>关联配置音量。</summary>
        public float Volume;
        /// <summary>事件 UTC ISO-8601 时间。</summary>
        public string TimestampUtc;
    }
}
#endif
