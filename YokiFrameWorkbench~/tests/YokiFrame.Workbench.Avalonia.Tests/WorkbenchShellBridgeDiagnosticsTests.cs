using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench Shell 对桥接诊断摘要的 Avalonia 投影。
/// </summary>
public sealed class WorkbenchShellBridgeDiagnosticsTests
{
    /// <summary>
    /// 验证 Workbench 引擎诊断卡片压缩为首屏可扫读的关键 FileBridge 字段。
    /// </summary>
    [Fact]
    public void WorkbenchShellViewModelProjectsBridgeDiagnosticsIntoEngineCards()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);
        var status = CreateDiagnosticBridgeStatus();

        viewModel.UpdateDashboard(CreateDashboardState(status));

        Assert.InRange(viewModel.EngineCards.Count, 1, 4);
        AssertMetricCard(viewModel.SummaryCards, "队列", "2 / 1", "results 4 / deadletter 3");
        AssertMetricCard(viewModel.EngineCards, "事件", "JSONL", "protocol 12 files / 4096 bytes");
        AssertMetricCard(viewModel.EngineCards, "背压", "Active", "BridgeBusy 5");
    }

    /// <summary>
    /// 验证有错误或背压时，概要卡仍然能暴露最近问题，而不是完全吞掉异常状态。
    /// </summary>
    [Fact]
    public void WorkbenchShellViewModelKeepsRecentIssueInSummaryCards()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);
        var status = CreateDiagnosticBridgeStatus();

        viewModel.UpdateDashboard(CreateDashboardState(status));

        AssertMetricCard(viewModel.SummaryCards, "最近问题", "Bridge busy", "limit MaxPendingCommands");
    }

    /// <summary>
    /// 创建包含诊断摘要字段的 FileBridge 状态。
    /// </summary>
    /// <returns>测试用 FileBridge 状态。</returns>
    private static FileBridgeStatus CreateDiagnosticBridgeStatus()
    {
        return new FileBridgeStatus(
            "unity-editor",
            "F:/Project/.yokiframe/engines/unity-editor",
            "F:/Project/.yokiframe/engines/unity-editor/commands",
            "F:/Project/.yokiframe/engines/unity-editor/results")
        {
            PendingCount = 2,
            ProcessingCount = 1,
            DeadletterCount = 3,
            ResultCount = 4,
            ProtocolFileCount = 12,
            ProtocolBytes = 4096,
            OldestProtocolFileUtc = DateTimeOffset.Parse("2026-06-20T11:50:00.0000000Z"),
            BackpressureActive = true,
            LastPollLimitReason = "MaxPendingCommands",
            BridgeBusyCount = 5,
            LastError = "Bridge busy"
        };
    }

    /// <summary>
    /// 创建包含指定 FileBridge 状态的 dashboard 投影，供 ViewModel 测试使用。
    /// </summary>
    /// <param name="status">FileBridge 状态。</param>
    /// <returns>测试 dashboard 状态。</returns>
    private static WorkbenchDashboardState CreateDashboardState(FileBridgeStatus status)
    {
        WorkbenchBridgeHealth health = new(
            WorkbenchBridgeConnectionState.Online,
            "FileBridge is online for unity-editor.",
            "No action needed.",
            new[] { status.EngineRoot },
            1,
            15,
            "test-session",
            7,
            "EditMode",
            3);

        return new WorkbenchDashboardState(
            "F:/Project",
            DateTimeOffset.UtcNow,
            Array.Empty<EngineRegistryEntry>(),
            "unity-editor",
            status,
            health,
            null,
            Array.Empty<WorkbenchSnapshotState>(),
            "{}",
            Array.Empty<string>());
    }

    /// <summary>
    /// 断言指定标题的状态卡片包含预期主值和详情。
    /// </summary>
    /// <param name="cards">卡片集合。</param>
    /// <param name="title">卡片标题。</param>
    /// <param name="value">预期主值。</param>
    /// <param name="detail">预期详情。</param>
    private static void AssertMetricCard(
        IReadOnlyList<WorkbenchMetricCard> cards,
        string title,
        string value,
        string detail)
    {
        var card = Assert.Single(cards, item => item.Title == title);
        Assert.Equal(value, card.Value);
        Assert.Equal(detail, card.Detail);
    }
}
