namespace YokiFrame.Tooling.Application.Services;

/// <summary>
/// EventKit 周期投影已迁至 <see cref="WorkbenchDashboardKitProjections"/>；
/// Telemetry / CodeLocation 等专用路径仍在同类型其它 partial。
/// </summary>
public sealed partial class WorkbenchDashboardService
{
    private const string EVENT_KIT = "EventKit";
}
