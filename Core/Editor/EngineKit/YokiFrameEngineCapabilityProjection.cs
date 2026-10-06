#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 能力矩阵投影：把操作集合投影为"操作 × 目标"的可用性条目。
    /// </summary>
    /// <remarks>
    /// `engine_capabilities` 与 Engine state snapshot 共用这一份实现，避免两处对可用性的判定漂移；
    /// 判定本身仍来自 <see cref="YokiFrameEngineAvailabilityRules"/>（与 Gate 同源）。
    /// </remarks>
    internal static class YokiFrameEngineCapabilityProjection
    {
        /// <summary>投影全部操作。</summary>
        /// <param name="operations">操作集合。</param>
        /// <param name="settings">开关快照。</param>
        /// <param name="hostTargets">宿主承载目标。</param>
        /// <returns>能力条目。</returns>
        public static IReadOnlyList<YokiFrameEngineOperationCapability> Create(
            IReadOnlyList<IYokiFrameEngineOperation> operations,
            YokiFrameEngineSettingsSnapshot settings,
            YokiFrameEngineExecutionTarget hostTargets)
        {
            var capabilities = new List<YokiFrameEngineOperationCapability>(operations.Count);
            for (var index = 0; index < operations.Count; index++)
            {
                YokiFrameEngineOperationDescriptor descriptor = operations[index].Descriptor;
                capabilities.Add(new YokiFrameEngineOperationCapability
                {
                    Action = descriptor.Action,
                    Kind = descriptor.Kind.ToString(),
                    DeclaredTargets = YokiFrameEngineExecutionTargets.Format(descriptor.Targets),
                    IsDiagnostic = descriptor.IsDiagnostic,
                    IsCancellation = descriptor.IsCancellation,
                    ExemptFromExecutionSwitch = descriptor.ExemptFromExecutionSwitch,
                    IsTargetAgnostic = descriptor.IsTargetAgnostic,
                    TargetAvailability = ResolveTargets(descriptor, settings, hostTargets)
                });
            }

            return capabilities;
        }

        /// <summary>展开单个操作的逐目标可用性。</summary>
        /// <param name="descriptor">操作描述。</param>
        /// <param name="settings">开关快照。</param>
        /// <param name="hostTargets">宿主承载目标。</param>
        /// <returns>逐目标可用性。</returns>
        private static IReadOnlyList<YokiFrameEngineTargetAvailability> ResolveTargets(
            YokiFrameEngineOperationDescriptor descriptor,
            YokiFrameEngineSettingsSnapshot settings,
            YokiFrameEngineExecutionTarget hostTargets)
        {
            IReadOnlyList<YokiFrameEngineExecutionTarget> targets = YokiFrameEngineExecutionTargets.Enumerate(descriptor.Targets);
            var availability = new List<YokiFrameEngineTargetAvailability>(targets.Count);
            for (var index = 0; index < targets.Count; index++)
            {
                YokiFrameEngineExecutionTarget target = targets[index];
                availability.Add(new YokiFrameEngineTargetAvailability
                {
                    Target = YokiFrameEngineExecutionTargets.Format(target),
                    Availability = YokiFrameEngineAvailabilityRules.Resolve(descriptor, target, settings, hostTargets).ToString()
                });
            }

            return availability;
        }
    }
}
#endif