#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Engine Kit 内建结果载荷的写出器；实际拼装复用 <see cref="YokiFrameEngineJsonBuilder"/>。
    /// </summary>
    internal static class YokiFrameEngineJsonWriter
    {
        /// <summary>写出 domain_state 载荷。</summary>
        /// <param name="state">引擎状态。</param>
        /// <param name="settings">开关快照。</param>
        /// <returns>JSON 文本。</returns>
        public static string WriteDomainState(
            YokiFrameEngineDomainState state,
            YokiFrameEngineSettingsSnapshot settings)
        {
            return WriteDomainStateInto(new YokiFrameEngineJsonBuilder(), state, settings).EndObject().ToString();
        }

        /// <summary>
        /// 把 domain_state 字段写入既有 builder，供 snapshot 在同一个对象里追加更多字段。
        /// </summary>
        /// <param name="builder">目标 builder。</param>
        /// <param name="state">引擎状态。</param>
        /// <param name="settings">开关快照。</param>
        /// <returns>同一个 builder。</returns>
        internal static YokiFrameEngineJsonBuilder WriteDomainStateInto(
            YokiFrameEngineJsonBuilder builder,
            YokiFrameEngineDomainState state,
            YokiFrameEngineSettingsSnapshot settings)
        {
            return builder
                .StartObject()
                .Property("engineKind", state.EngineKind)
                .Property("engineVersion", state.EngineVersion)
                .Property("mode", state.Mode)
                .Property("activeTarget", state.ActiveTarget)
                .Property("isPlaying", state.IsPlaying)
                .Property("isCompiling", state.IsCompiling)
                .Property("isBusy", state.IsBusy)
                .Property("sessionId", state.SessionId)
                .Property("generation", state.Generation)
                .Property("sessionIdentityAvailable", state.SessionIdentityAvailable)
                .Property("settingsState", settings.State.ToString())
                .Property("settingsReason", settings.Reason);
        }

        /// <summary>把能力条目数组写入当前对象。</summary>
        /// <param name="builder">目标 builder。</param>
        /// <param name="propertyName">数组键名。</param>
        /// <param name="capabilities">能力条目。</param>
        internal static void WriteCapabilityArray(
            YokiFrameEngineJsonBuilder builder,
            string propertyName,
            IReadOnlyList<YokiFrameEngineOperationCapability> capabilities)
        {
            builder.Name(propertyName).StartArray();
            for (var index = 0; index < capabilities.Count; index++)
            {
                YokiFrameEngineOperationCapability capability = capabilities[index];
                builder.StartObject()
                    .Property("action", capability.Action)
                    .Property("kind", capability.Kind)
                    .Property("targets", capability.DeclaredTargets)
                    .Property("isDiagnostic", capability.IsDiagnostic)
                    .Property("isCancellation", capability.IsCancellation)
                    .Property("exemptFromExecutionSwitch", capability.ExemptFromExecutionSwitch)
                    .Property("isTargetAgnostic", capability.IsTargetAgnostic)
                    .Name("targetAvailability")
                    .StartArray();
                IReadOnlyList<YokiFrameEngineTargetAvailability> availability = capability.TargetAvailability;
                for (var targetIndex = 0; targetIndex < availability.Count; targetIndex++)
                {
                    builder.StartObject()
                        .Property("target", availability[targetIndex].Target)
                        .Property("availability", availability[targetIndex].Availability)
                        .EndObject();
                }

                builder.EndArray().EndObject();
            }

            builder.EndArray();
        }

        /// <summary>写出 engine_capabilities 载荷。</summary>
        /// <param name="report">能力报告。</param>
        /// <returns>JSON 文本。</returns>
        public static string WriteCapabilityReport(YokiFrameEngineCapabilityReport report)
        {
            YokiFrameEngineJsonBuilder builder = new YokiFrameEngineJsonBuilder()
                .StartObject()
                .Property("engineKind", report.EngineKind)
                .Property("hostTargets", report.HostTargets)
                .Property("settingsState", report.SettingsState)
                .Property("settingsReason", report.SettingsReason)
                .Property("executionBlocked", report.ExecutionBlocked)
                .Name("operations")
                .StartArray();
            IReadOnlyList<YokiFrameEngineOperationCapability> operations = report.Operations;
            for (var index = 0; index < operations.Count; index++)
            {
                YokiFrameEngineOperationCapability capability = operations[index];
                builder.StartObject()
                    .Property("action", capability.Action)
                    .Property("kind", capability.Kind)
                    .Property("targets", capability.DeclaredTargets)
                    .Property("isDiagnostic", capability.IsDiagnostic)
                    .Property("isCancellation", capability.IsCancellation)
                    .Property("exemptFromExecutionSwitch", capability.ExemptFromExecutionSwitch)
                    .Property("isTargetAgnostic", capability.IsTargetAgnostic)
                    .Name("targetAvailability")
                    .StartArray();
                IReadOnlyList<YokiFrameEngineTargetAvailability> availability = capability.TargetAvailability;
                for (var targetIndex = 0; targetIndex < availability.Count; targetIndex++)
                {
                    builder.StartObject()
                        .Property("target", availability[targetIndex].Target)
                        .Property("availability", availability[targetIndex].Availability)
                        .EndObject();
                }

                builder.EndArray().EndObject();
            }

            return builder.EndArray().EndObject().ToString();
        }
    }
}
#endif