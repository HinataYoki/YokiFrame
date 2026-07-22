#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>描述逻辑总线配置、有效音量和 active voice 数量。</summary>
    public sealed class AudioBusSnapshot
    {
        /// <summary>逻辑总线名称。</summary>
        public string Name;
        /// <summary>不受静音影响的配置音量。</summary>
        public float Volume;
        /// <summary>考虑静音后的有效音量。</summary>
        public float EffectiveVolume;
        /// <summary>总线是否静音。</summary>
        public bool Muted;
        /// <summary>是否为 Master 总线。</summary>
        public bool IsMaster;
        /// <summary>是否为 Master 或框架内置可播放总线。</summary>
        public bool IsBuiltIn;
        /// <summary>是否为内置总线或由项目显式注册。</summary>
        public bool IsRegistered;
        /// <summary>当前 active voice 数量。</summary>
        public int ActiveVoiceCount;
    }
}
#endif
