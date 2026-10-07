#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 描述 RoslynKit 执行开关的读取结果；缺失与解析失败都按关闭处理，但保留可诊断的原因。
    /// </summary>
    public enum RoslynSettingsState
    {
        /// <summary>配置存在且已开启。</summary>
        Enabled,

        /// <summary>配置存在且明确关闭。</summary>
        Disabled,

        /// <summary>配置文件不存在。</summary>
        MissingConfig,

        /// <summary>配置文件存在但无法解析。</summary>
        InvalidConfig
    }

    /// <summary>
    /// 表示一次开关读取的快照；Gate 依赖它决定是否放行执行类操作。
    /// </summary>
    public sealed class RoslynSettingsSnapshot
    {
        private RoslynSettingsSnapshot(RoslynSettingsState state, string reason)
        {
            State = state;
            Reason = reason ?? string.Empty;
        }

        /// <summary>获取开关状态。</summary>
        public RoslynSettingsState State { get; }

        /// <summary>获取可诊断的原因说明；无附加信息时为空字符串。</summary>
        public string Reason { get; }

        /// <summary>获取开关是否允许执行类操作。</summary>
        public bool IsEnabled
        {
            get { return State == RoslynSettingsState.Enabled; }
        }

        /// <summary>获取当前状态是否阻断执行类操作（fail-closed：只有 Enabled 放行）。</summary>
        public bool BlocksExecution
        {
            get { return State != RoslynSettingsState.Enabled; }
        }

        /// <summary>创建已开启快照。</summary>
        /// <param name="reason">可选的诊断说明。</param>
        /// <returns>已开启快照。</returns>
        public static RoslynSettingsSnapshot Enabled(string reason = "")
        {
            return new RoslynSettingsSnapshot(RoslynSettingsState.Enabled, reason);
        }

        /// <summary>创建明确关闭快照。</summary>
        /// <param name="reason">可选的诊断说明。</param>
        /// <returns>关闭快照。</returns>
        public static RoslynSettingsSnapshot Disabled(string reason = "")
        {
            return new RoslynSettingsSnapshot(RoslynSettingsState.Disabled, reason);
        }

        /// <summary>创建配置缺失快照。</summary>
        /// <param name="reason">可选的诊断说明。</param>
        /// <returns>配置缺失快照。</returns>
        public static RoslynSettingsSnapshot MissingConfig(string reason = "")
        {
            return new RoslynSettingsSnapshot(RoslynSettingsState.MissingConfig, reason);
        }

        /// <summary>创建配置解析失败快照。</summary>
        /// <param name="reason">可选的诊断说明。</param>
        /// <returns>配置解析失败快照。</returns>
        public static RoslynSettingsSnapshot InvalidConfig(string reason = "")
        {
            return new RoslynSettingsSnapshot(RoslynSettingsState.InvalidConfig, reason);
        }
    }

    /// <summary>
    /// 定义 RoslynKit 执行开关的读取端口；宿主与测试各自提供实现，Gate 每次执行都读取一次。
    /// </summary>
    public interface IRoslynSettingsSource
    {
        /// <summary>读取当前开关快照；实现负责缓存与失效策略。</summary>
        /// <returns>当前开关快照。</returns>
        RoslynSettingsSnapshot Read();
    }
}
#endif
