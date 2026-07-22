using YokiFrame.Protocol.FileBridge;
using YokiFrame.Tooling.Application.Engines;
using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>
/// 负责 Dashboard 在 engine 尚未选中时生成可恢复的应用层状态。
/// </summary>
public sealed partial class WorkbenchDashboardService
{
    /// <summary>
    /// 创建等待宿主或用户选择的 dashboard 状态，并跳过所有 engine 专属 IO。
    /// </summary>
    /// <param name="generatedAtUtc">本轮状态生成时间。</param>
    /// <param name="engines">当前 registry 条目。</param>
    /// <param name="engineSelection">未完成的 engine 选择结果。</param>
    /// <param name="harnessSummary">与 engine 无关的 harness 摘要。</param>
    /// <param name="errors">读取 registry 或 harness 时产生的非终止错误。</param>
    /// <returns>可供 Workbench 恢复的 dashboard 状态。</returns>
    private WorkbenchDashboardState CreatePendingSelectionState(
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<EngineRegistryEntry> engines,
        EngineSelectionResult engineSelection,
        string harnessSummary,
        IReadOnlyList<string> errors)
    {
        return new WorkbenchDashboardState(
            mClient.Paths.ProjectRoot,
            generatedAtUtc,
            engines,
            engineSelection,
            null,
            CreatePendingSelectionHealth(engineSelection),
            null,
            Array.Empty<WorkbenchSnapshotState>(),
            harnessSummary,
            errors);
    }

    /// <summary>
    /// 把 engine 选择失败转换为 Workbench 可直接展示的连接健康信息。
    /// </summary>
    /// <param name="engineSelection">未完成的 engine 选择结果。</param>
    /// <returns>不依赖具体 engine 路径的健康信息。</returns>
    private static WorkbenchBridgeHealth CreatePendingSelectionHealth(EngineSelectionResult engineSelection)
    {
        var error = engineSelection.Error!;
        var state = engineSelection.Status == EngineSelectionStatus.SelectionRequired
            ? WorkbenchBridgeConnectionState.EngineSelectionRequired
            : WorkbenchBridgeConnectionState.EngineUnavailable;
        return new WorkbenchBridgeHealth(
            state,
            error.Message,
            error.Suggestion,
            error.EvidencePaths,
            null,
            (long)HeartbeatStaleThreshold.TotalSeconds,
            string.Empty,
            0L,
            string.Empty,
            0L);
    }
}
