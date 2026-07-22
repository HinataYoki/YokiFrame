namespace YokiFrame.Tooling.Application.Models.FsmKit;

/// <summary>
/// FsmKit Workbench 只读展示文案与数据通道投影；无 UI 依赖，供 Avalonia 与测试共用。
/// </summary>
public static class WorkbenchFsmKitPresentation
{
    /// <summary>
    /// 把空字段统一显示为“未知”，避免来源头部出现空白。
    /// </summary>
    /// <param name="value">原始字段。</param>
    /// <returns>可显示文本。</returns>
    public static string CreateOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "未知" : value;
    }

    /// <summary>
    /// 把协议来源和命令传输转换为紧凑页头可读的数据通道。
    /// </summary>
    /// <param name="source">telemetry / snapshot / command 等。</param>
    /// <param name="transport">命令实际传输；周期读取可为空。</param>
    /// <returns>用户可见通道名。</returns>
    public static string CreateDataChannelText(string? source, string? transport)
    {
        if (string.Equals(source, "telemetry", StringComparison.OrdinalIgnoreCase))
        {
            return "Shared Memory";
        }

        if (string.Equals(source, "snapshot", StringComparison.OrdinalIgnoreCase))
        {
            return "文件 Snapshot";
        }

        if (string.Equals(source, "command", StringComparison.OrdinalIgnoreCase))
        {
            string transportText = transport ?? string.Empty;
            return transportText.Contains("fast", StringComparison.OrdinalIgnoreCase)
                ? "FastChannel"
                : "FileBridge";
        }

        return string.IsNullOrWhiteSpace(source) ? "等待数据" : source;
    }

    /// <summary>
    /// 创建页面诊断摘要，优先显示 stale 原因和详情缺失。
    /// </summary>
    /// <param name="state">完整 FsmKit 状态。</param>
    /// <param name="hasSelectedSummary">左侧列表是否已选中实例摘要。</param>
    /// <param name="hasSelectedDetails">当前是否持有该实例的完整详情。</param>
    /// <returns>诊断文案。</returns>
    public static string CreateDiagnosticText(
        WorkbenchFsmKitState state,
        bool hasSelectedSummary,
        bool hasSelectedDetails)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!string.IsNullOrWhiteSpace(state.StaleReason))
        {
            return "数据已回落或过期: " + state.StaleReason;
        }

        if (!hasSelectedSummary)
        {
            return "当前宿主没有活动 FSM 实例。";
        }

        return hasSelectedDetails
            ? "只读诊断已同步，转换边表示已观测历史，不代表静态许可规则。"
            : "已选择实例摘要；选择列表项可按 instanceId 查询完整详情。";
    }
}
