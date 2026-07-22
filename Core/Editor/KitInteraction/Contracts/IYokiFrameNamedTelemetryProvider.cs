#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 为一个 Kit 提供按安全名称拆分的 Shared Memory latest frame，避免把多个实例详情塞入单个 64 KiB payload。
    /// </summary>
    public interface IYokiFrameNamedTelemetryProvider : IYokiFrameKitInteractionProvider
    {
        /// <summary>
        /// 获取当前活动的命名 Telemetry 名称；名称必须满足 FileBridge SafeId 约束。
        /// </summary>
        IReadOnlyList<string> TelemetryNames { get; }

        /// <summary>
        /// 创建指定名称的完整 latest-frame payload。
        /// </summary>
        /// <param name="name">已经由 <see cref="TelemetryNames"/> 声明的安全名称。</param>
        /// <returns>Kit 自有 schema 的 JSON payload。</returns>
        string CreateTelemetry(string name);
    }
}
#endif
