namespace YokiFrame.Tooling.Application.Services;

/// <summary>
/// 声明 Workbench 首屏 Dashboard 需要统一读取的 Runtime Interaction Kit 目录。
/// 新增已完成 Interaction 的 Kit 时，只在此处登记一次，避免 LoadDashboard 与 telemetry 列表漂移。
/// </summary>
internal static class WorkbenchRuntimeKitCatalog
{
    /// <summary>
    /// 获取 Dashboard 需要读取 state Snapshot 的目录；System 由 Dashboard 单独读取，不在此列表。
    /// 文件型 Kit 保留在此目录，但不会进入 Shared Memory Telemetry 读取路径。
    /// </summary>
    internal static IReadOnlyList<string> SnapshotStateKits { get; } = new[]
    {
        "Architecture",
        "FsmKit",
        "EventKit",
        "LogKit",
        "PoolKit",
        "ResKit",
        "ActionKit",
        "AudioKit",
        "SpatialKit",
        "SaveKit",
        "UIKit",
    };

    /// <summary>获取允许周期性 Shared Memory 读取的 Runtime Kit 目录。</summary>
    internal static IReadOnlyList<string> TelemetryStateKits { get; } = new[]
    {
        "Architecture",
        "FsmKit",
        "EventKit",
        "LogKit",
        "PoolKit",
        "ResKit",
        "ActionKit",
        "AudioKit",
        "SpatialKit",
        "UIKit",
    };

    /// <summary>判断指定 state 是否允许优先读取 Shared Memory Telemetry。</summary>
    /// <param name="kit">稳定 Kit 标识。</param>
    /// <returns>属于高速 Telemetry 目录时返回 true。</returns>
    internal static bool UsesTelemetryState(string kit)
    {
        for (var index = 0; index < TelemetryStateKits.Count; index++)
        {
            if (string.Equals(TelemetryStateKits[index], kit, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
