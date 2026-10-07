#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// RoslynKit 的目标可用性判定规则；Gate 与 engine_capabilities 共用这一份实现，避免两处漂移。
    /// </summary>
    internal static class RoslynAvailabilityRules
    {
        /// <summary>
        /// 解析单个操作在指定目标下的可用性。
        /// </summary>
        /// <remarks>
        /// 判定顺序与 Gate 一致：执行开关 → 目标无关豁免 → 操作声明目标 → 宿主承载目标。
        /// 目标无关操作（诊断与能力查询）只受执行开关约束。
        /// </remarks>
        /// <param name="descriptor">操作描述。</param>
        /// <param name="target">请求的单一目标。</param>
        /// <param name="settings">执行开关快照。</param>
        /// <param name="hostTargets">宿主可承载的目标。</param>
        /// <returns>可用性；<see cref="RoslynOperationAvailability.Enabled"/> 表示可执行。</returns>
        public static RoslynOperationAvailability Resolve(
            RoslynOperationDescriptor descriptor,
            RoslynExecutionTarget target,
            RoslynSettingsSnapshot settings,
            RoslynExecutionTarget hostTargets)
        {
            if (!descriptor.ExemptFromExecutionSwitch && (settings.BlocksExecution || !descriptor.ExecutionPermitted))
            {
                return RoslynOperationAvailability.DisabledBySettings;
            }

            if (descriptor.IsTargetAgnostic)
            {
                return RoslynOperationAvailability.Enabled;
            }

            if ((descriptor.Targets & target) == 0)
            {
                return RoslynOperationAvailability.UnsupportedByTarget;
            }

            return (hostTargets & target) == 0
                ? RoslynOperationAvailability.UnsupportedByHostTarget
                : RoslynOperationAvailability.Enabled;
        }
    }
}
#endif
