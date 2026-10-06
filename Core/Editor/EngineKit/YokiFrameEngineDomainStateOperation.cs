#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// domain_state：报告宿主引擎状态与开关状态；诊断类动作，不受执行开关阻断。
    /// </summary>
    public sealed class YokiFrameEngineDomainStateOperation : IYokiFrameEngineOperation
    {
        /// <summary>action 标识。</summary>
        public const string ACTION = "domain_state";

        private readonly IYokiFrameEngineOperationProvider mProvider;
        private readonly IYokiFrameEngineSettingsSource mSettingsSource;

        /// <summary>创建 domain_state 操作。</summary>
        /// <param name="provider">引擎侧 Provider。</param>
        /// <param name="settingsSource">执行开关读取端口。</param>
        public YokiFrameEngineDomainStateOperation(
            IYokiFrameEngineOperationProvider provider,
            IYokiFrameEngineSettingsSource settingsSource)
        {
            mProvider = provider ?? throw new System.ArgumentNullException(nameof(provider));
            mSettingsSource = settingsSource ?? throw new System.ArgumentNullException(nameof(settingsSource));
            Descriptor = new YokiFrameEngineOperationDescriptor(
                ACTION,
                YokiFrameCommandKind.ReadOnly,
                isDiagnostic: true,
                isCancellation: false,
                targets: YokiFrameEngineExecutionTargets.ALL,
                isTargetAgnostic: true);
        }

        /// <summary>获取操作描述。</summary>
        public YokiFrameEngineOperationDescriptor Descriptor { get; }

        /// <summary>返回引擎状态 JSON。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令结果。</returns>
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            YokiFrameEngineDomainState state = mProvider.ReadDomainState();
            YokiFrameEngineSettingsSnapshot settings = mSettingsSource.Read();
            return YokiFrameCommandResult.Success(YokiFrameEngineJsonWriter.WriteDomainState(state, settings));
        }
    }
}
#endif