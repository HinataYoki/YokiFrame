using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// Shell 页眉/状态栏/命令预览等纯文本投影，与导航和页面生命周期分离。
/// </summary>
public sealed partial class WorkbenchShellViewModel
{
    /// <summary>
    /// 创建命令响应的日志预览，避免较大的 JSON 响应撑开运行日志行。
    /// </summary>
    /// <param name="resultJson">Runtime 返回的 result JSON。</param>
    /// <returns>可显示在运行日志中的短文本。</returns>
    private static string CreateCommandResultPreview(string resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return "{}";
        }

        return resultJson.Length <= MAX_COMMAND_RESULT_LOG_LENGTH
            ? resultJson
            : resultJson[..MAX_COMMAND_RESULT_LOG_LENGTH] + "...";
    }

    /// <summary>
    /// 创建顶部状态栏文本。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>状态栏文本。</returns>
    private static string CreateHeaderText(WorkbenchDashboardState state)
    {
        var mode = string.IsNullOrWhiteSpace(state.BridgeHealth.Mode) ? "unknown" : state.BridgeHealth.Mode;
        var engineId = string.IsNullOrWhiteSpace(state.SelectedEngineId) ? "not selected" : state.SelectedEngineId;
        return "Engine: " + engineId
            + " | Mode: " + mode
            + " | Bridge: " + state.BridgeHealth.State;
    }

    /// <summary>
    /// 创建底部状态文本。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>底部状态文本。</returns>
    private static string CreateStatusText(WorkbenchDashboardState state)
    {
        var reconnect = state.BridgeHealth.RequiresReconnect ? "reconnect needed" : "connected";
        return "generated " + state.GeneratedAtUtc.ToLocalTime().ToString("HH:mm:ss")
            + " | " + reconnect
            + " | snapshots " + state.Snapshots.Count(snapshot => snapshot.Exists) + "/" + state.Snapshots.Count;
    }
}
