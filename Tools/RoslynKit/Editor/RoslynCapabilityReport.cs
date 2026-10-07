#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 描述单个操作在某个目标下的可用性。
    /// </summary>
    public enum RoslynOperationAvailability
    {
        /// <summary>当前可执行。</summary>
        Enabled,

        /// <summary>执行开关关闭、配置缺失或解析失败（诊断与取消类不受此限）。</summary>
        DisabledBySettings,

        /// <summary>操作自身未声明该目标。</summary>
        UnsupportedByTarget,

        /// <summary>当前宿主不承载该目标。</summary>
        UnsupportedByHostTarget
    }

    /// <summary>
    /// 单个操作在单个目标下的可用性条目。
    /// </summary>
    public sealed class RoslynTargetAvailability
    {
        /// <summary>目标 wire 文本。</summary>
        public string Target = string.Empty;

        /// <summary>该目标下的可用性文本。</summary>
        public string Availability = string.Empty;
    }

    /// <summary>
    /// 单个操作的能力条目：声明信息 + 逐目标可用性矩阵。
    /// </summary>
    public sealed class RoslynOperationCapability
    {
        /// <summary>action 标识。</summary>
        public string Action = string.Empty;

        /// <summary>风险等级文本。</summary>
        public string Kind = string.Empty;

        /// <summary>该操作声明的目标 wire 文本（能力声明，可用于组合）。</summary>
        public string DeclaredTargets = string.Empty;

        /// <summary>是否为诊断/查询类。</summary>
        public bool IsDiagnostic;

        /// <summary>是否为取消类。</summary>
        public bool IsCancellation;

        /// <summary>是否豁免执行开关。</summary>
        public bool ExemptFromExecutionSwitch;

        /// <summary>是否为目标无关操作。</summary>
        public bool IsTargetAgnostic;

        /// <summary>逐目标可用性；目标无关操作列出声明目标但忽略宿主承载限制。</summary>
        public IReadOnlyList<RoslynTargetAvailability> TargetAvailability = new List<RoslynTargetAvailability>();
    }

    /// <summary>
    /// engine_capabilities 动作的返回模型：开关状态 + 宿主承载的目标 + 操作 × 目标可用性矩阵。
    /// </summary>
    public sealed class RoslynCapabilityReport
    {
        /// <summary>引擎类型。</summary>
        public string EngineKind = string.Empty;

        /// <summary>宿主能承载的目标 wire 文本。</summary>
        public string HostTargets = string.Empty;

        /// <summary>执行开关状态文本。</summary>
        public string SettingsState = string.Empty;

        /// <summary>开关状态的诊断原因。</summary>
        public string SettingsReason = string.Empty;

        /// <summary>开关是否阻断执行类操作。</summary>
        public bool ExecutionBlocked;

        /// <summary>全部操作的能力条目。</summary>
        public IReadOnlyList<RoslynOperationCapability> Operations = new List<RoslynOperationCapability>();
    }
}
#endif