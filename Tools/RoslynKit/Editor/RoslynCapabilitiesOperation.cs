#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// engine_capabilities：报告操作 × 目标的可用性矩阵与开关状态；诊断类且目标无关，不受执行开关阻断。
    /// </summary>
    public sealed class RoslynCapabilitiesOperation : IRoslynOperation
    {
        /// <summary>action 标识。</summary>
        public const string ACTION = "engine_capabilities";

        private readonly IRoslynOperationProvider mProvider;
        private readonly IRoslynSettingsSource mSettingsSource;
        private readonly System.Func<IReadOnlyList<IRoslynOperation>> mOperationsAccessor;

        /// <summary>创建 engine_capabilities 操作。</summary>
        /// <param name="provider">引擎侧 Provider。</param>
        /// <param name="settingsSource">执行开关读取端口。</param>
        /// <param name="operationsAccessor">返回完整操作集合的回调；由 Kit Provider 注入，避免自引用环。</param>
        public RoslynCapabilitiesOperation(
            IRoslynOperationProvider provider,
            IRoslynSettingsSource settingsSource,
            System.Func<IReadOnlyList<IRoslynOperation>> operationsAccessor)
        {
            mProvider = provider ?? throw new System.ArgumentNullException(nameof(provider));
            mSettingsSource = settingsSource ?? throw new System.ArgumentNullException(nameof(settingsSource));
            mOperationsAccessor = operationsAccessor ?? throw new System.ArgumentNullException(nameof(operationsAccessor));
            Descriptor = new RoslynOperationDescriptor(
                ACTION,
                YokiFrameCommandKind.ReadOnly,
                isDiagnostic: true,
                isCancellation: false,
                targets: RoslynExecutionTargets.ALL,
                isTargetAgnostic: true);
        }

        /// <summary>获取操作描述。</summary>
        public RoslynOperationDescriptor Descriptor { get; }

        /// <summary>返回逐 target 能力矩阵 JSON。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令结果。</returns>
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            RoslynSettingsSnapshot settings = mSettingsSource.Read();
            RoslynExecutionTarget hostTargets = mProvider.HostTargets;
            IReadOnlyList<IRoslynOperation> operations = mOperationsAccessor();

            // 与 state snapshot 共用同一份投影，两处不会漂移。
            IReadOnlyList<RoslynOperationCapability> capabilities =
                RoslynCapabilityProjection.Create(operations, settings, hostTargets);

            var report = new RoslynCapabilityReport
            {
                EngineKind = mProvider.EngineKind,
                HostTargets = RoslynExecutionTargets.Format(hostTargets),
                SettingsState = settings.State.ToString(),
                SettingsReason = settings.Reason,
                ExecutionBlocked = settings.BlocksExecution,
                Operations = capabilities
            };
            return YokiFrameCommandResult.Success(RoslynJsonWriter.WriteCapabilityReport(report));
        }
    }
}
#endif